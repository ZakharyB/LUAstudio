using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.ExecutionHost.Runtime;

namespace LUAstudio.ExecutionHost.Sessions;

public sealed class ExecutionSession : IDisposable
{
    private readonly SessionConfiguration _configuration;
    private readonly Action<SandboxEnvelope> _publish;
    private readonly LuauDebugController _debug;
    private readonly ModuleResolver _modules;
    private readonly ExecutionTraceRecorder _trace;
    private readonly LuauRuntime _runtime;
    private readonly CancellationTokenSource _sessionCts = new();
    private Task? _executionTask;
    private string? _sourcePath;
    private Stopwatch? _stopwatch;

    public ExecutionSession(Guid sessionId, SessionConfiguration configuration, Action<SandboxEnvelope> publish)
    {
        SessionId = sessionId;
        _configuration = configuration;
        _publish = publish;
        _debug = new LuauDebugController();
        _modules = new ModuleResolver();
        _trace = new ExecutionTraceRecorder();
        _runtime = new LuauRuntime(_debug, _modules, configuration, _trace);
        // A created session must be ready for LoadScript. Previously the Luau
        // state was only created after an optional ConfigureEnvironment command,
        // so the normal Create -> Load -> Execute flow always failed.
        _runtime.Initialize(configuration.EnableRobloxMocks);
        _runtime.Output += (_, text) => _publish(new SandboxEnvelope(
            SandboxMessageKind.OutputLog,
            SessionId,
            null,
            new OutputLogPayload(SessionId, "stdout", text)));

        _debug.Paused += (line, path, reason) =>
        {
            State = reason.StartsWith("step", StringComparison.Ordinal)
                ? ExecutionSessionState.Stepping
                : ExecutionSessionState.Paused;

            PublishStateChanged();

            if (reason.StartsWith("step", StringComparison.Ordinal))
            {
                _publish(new SandboxEnvelope(
                    SandboxMessageKind.StepCompleted,
                    SessionId,
                    null,
                    new StepCompletedPayload(SessionId, line, path ?? _sourcePath, reason)));
            }
            else
            {
                _publish(new SandboxEnvelope(
                    SandboxMessageKind.BreakpointHit,
                    SessionId,
                    null,
                    new BreakpointHitPayload(SessionId, line, path ?? _sourcePath, reason)));
            }
        };
    }

    public Guid SessionId { get; }

    public ExecutionSessionState State { get; private set; } = ExecutionSessionState.Created;

    public void ConfigureEnvironment(string profile, bool enableRobloxMocks, bool allowNetwork)
    {
        EnsureState(ExecutionSessionState.Created, ExecutionSessionState.Loaded, ExecutionSessionState.Stopped);
        _runtime.Initialize(enableRobloxMocks);
        State = ExecutionSessionState.Created;
    }

    public void SetWorkspaceModules(IReadOnlyList<WorkspaceModuleEntry> modules)
    {
        _modules.SetModules(modules.Select(m => (m.Path, m.Source)).ToList());
    }

    public void LoadModule(string path, string source) => _modules.SetModule(path, source);

    public void LoadScript(string source, string? sourcePath)
    {
        EnsureState(ExecutionSessionState.Created, ExecutionSessionState.Loaded, ExecutionSessionState.Stopped);
        _sourcePath = sourcePath;
        _runtime.LoadScript(source, sourcePath);
        State = ExecutionSessionState.Loaded;
    }

    public void SetBreakpoints(string? sourcePath, IReadOnlyList<BreakpointSpec> breakpoints) =>
        _debug.SetBreakpoints(sourcePath ?? _sourcePath, breakpoints);

    public void Execute()
    {
        EnsureLoaded();
        if (_executionTask is { IsCompleted: false })
        {
            throw new InvalidOperationException("Session is already executing.");
        }

        _stopwatch = Stopwatch.StartNew();
        State = ExecutionSessionState.Running;
        _debug.ResetExecutionControl();
        PublishStateChanged();
        _publish(new SandboxEnvelope(
            SandboxMessageKind.SessionStarted,
            SessionId,
            null,
            new SessionStartedPayload(SessionId, State)));

        _executionTask = Task.Run(() => RunExecutionAsync(_sessionCts.Token));
    }

    public void Continue()
    {
        State = ExecutionSessionState.Running;
        PublishStateChanged();
        _debug.Continue();
    }

    public void Pause() => _debug.RequestPause();

    public void StepOver()
    {
        State = ExecutionSessionState.Stepping;
        PublishStateChanged();
        _debug.StepOver();
    }

    public void StepInto()
    {
        State = ExecutionSessionState.Stepping;
        PublishStateChanged();
        _debug.StepInto();
    }

    public void StepOut()
    {
        State = ExecutionSessionState.Stepping;
        PublishStateChanged();
        _debug.StepOut();
    }

    public void Stop()
    {
        _debug.RequestStop();
        _debug.Interrupt();
        _sessionCts.Cancel();
        State = ExecutionSessionState.Stopped;
        PublishStateChanged();
        _publish(new SandboxEnvelope(
            SandboxMessageKind.SessionStopped,
            SessionId,
            null,
            new SessionStartedPayload(SessionId, State)));
    }

    public StackTraceResponsePayload GetStackTrace(int frameId) =>
        new(SessionId, _debug.GetStackFrames(_sourcePath));

    public ScopesResponsePayload GetScopes(int frameId) =>
        new(SessionId, _debug.GetScopes(frameId));

    public VariablesResponsePayload GetVariables(int variablesReference) =>
        new(SessionId, _debug.GetVariables(variablesReference));

    public EvaluateResultPayload Evaluate(int frameId, string expression)
    {
        try
        {
            var result = _debug.Evaluate(frameId, expression);
            return new EvaluateResultPayload(SessionId, result, null);
        }
        catch (Exception ex)
        {
            return new EvaluateResultPayload(SessionId, "nil", ex.Message);
        }
    }

    private async Task RunExecutionAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_configuration.ExecutionTimeoutMs);

        try
        {
            await Task.Run(() => _runtime.Execute(timeoutCts.Token), timeoutCts.Token).ConfigureAwait(false);
            Finish(ExecutionFinishReason.Completed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            State = ExecutionSessionState.TimedOut;
            PublishError("Execution timed out.", _sourcePath, 1, 1, "timeout");
            Finish(ExecutionFinishReason.Timeout);
        }
        catch (SandboxRuntimeException ex)
        {
            State = ExecutionSessionState.Crashed;
            _publish(new SandboxEnvelope(
                SandboxMessageKind.ErrorThrown,
                SessionId,
                null,
                new ErrorThrownPayload(SessionId, ex.ToErrorInfo())));
            Finish(ExecutionFinishReason.Error);
        }
        catch (Exception ex)
        {
            State = ExecutionSessionState.Crashed;
            PublishError(ex.Message, _sourcePath, 1, 1, "runtime");
            Finish(ExecutionFinishReason.Error);
        }
    }

    private void PublishError(string message, string? sourcePath, int line, int column, string errorKind)
    {
        _publish(new SandboxEnvelope(
            SandboxMessageKind.ErrorThrown,
            SessionId,
            null,
            new ErrorThrownPayload(SessionId, new ExecutionErrorInfo(
                message,
                sourcePath,
                line,
                column,
                BuildStackTrace(),
                errorKind))));
    }

    private IReadOnlyList<string> BuildStackTrace() =>
        _debug.GetStackFrames(_sourcePath)
            .Select(f => $"{f.Name} at {f.SourcePath}:{f.Line}")
            .ToArray();

    private void Finish(ExecutionFinishReason reason)
    {
        _stopwatch?.Stop();
        if (reason == ExecutionFinishReason.Completed)
        {
            State = ExecutionSessionState.Stopped;
        }

        try
        {
            var modules = _modules.Snapshot();
            var hash = _trace.ComputeModulesHash(modules);
            var snapshot = _trace.CreateSnapshot(SessionId, hash, Environment.TickCount);
            var traceDirectory = Path.Combine(Path.GetTempPath(), "LUAstudio", "traces");
            ExecutionTraceRecorder.SaveSnapshot(snapshot, traceDirectory);
        }
        catch
        {
            // Trace capture is best-effort and must not affect session teardown.
        }

        PublishStateChanged();
        _publish(new SandboxEnvelope(
            SandboxMessageKind.ExecutionFinished,
            SessionId,
            null,
            new ExecutionFinishedPayload(SessionId, reason, _stopwatch?.Elapsed.TotalMilliseconds ?? 0)));
    }

    private void PublishStateChanged() =>
        _publish(new SandboxEnvelope(
            SandboxMessageKind.ExecutionStateChanged,
            SessionId,
            null,
            new ExecutionStateChangedPayload(SessionId, State)));

    private void EnsureLoaded()
    {
        if (State != ExecutionSessionState.Loaded && State != ExecutionSessionState.Stopped)
        {
            if (State == ExecutionSessionState.Created)
            {
                throw new InvalidOperationException("LoadScript must be called before execution.");
            }
        }
    }

    private void EnsureState(params ExecutionSessionState[] allowed)
    {
        if (!allowed.Contains(State))
        {
            throw new InvalidOperationException($"Invalid session transition from {State}.");
        }
    }

    public void Dispose()
    {
        _sessionCts.Cancel();
        _sessionCts.Dispose();
        _runtime.Dispose();
    }
}

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<Guid, ExecutionSession> _sessions = new();

    public ExecutionSession Create(Guid sessionId, SessionConfiguration configuration, Action<SandboxEnvelope> publish)
    {
        var session = new ExecutionSession(sessionId, configuration, publish);
        if (!_sessions.TryAdd(sessionId, session))
        {
            session.Dispose();
            throw new InvalidOperationException($"Session '{sessionId}' already exists.");
        }

        session.ConfigureEnvironment(configuration.EnvironmentProfile, configuration.EnableRobloxMocks, configuration.AllowNetwork);
        return session;
    }

    public ExecutionSession Get(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var session)
            ? session
            : throw new KeyNotFoundException($"Session '{sessionId}' was not found.");

    public bool Stop(Guid sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return false;
        }

        session.Stop();
        session.Dispose();
        return true;
    }
}
