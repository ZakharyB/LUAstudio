using System.Collections.Concurrent;
using System.Diagnostics;
using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.ExecutionHost.Runtime;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.ExecutionHost.Sessions;

public sealed class ExecutionSession : IDisposable
{
    private readonly SessionConfiguration _configuration;
    private readonly Action<SandboxEnvelope> _publish;
    private readonly DebugController _debug;
    private readonly SandboxEnvironment _environment;
    private readonly InstrumentedAstInterpreter _interpreter;
    private readonly CancellationTokenSource _sessionCts = new();
    private ParseResult? _parseResult;
    private string _source = string.Empty;
    private Task? _executionTask;
    private string? _sourcePath;
    private Stopwatch? _stopwatch;

    public ExecutionSession(Guid sessionId, SessionConfiguration configuration, Action<SandboxEnvelope> publish)
    {
        SessionId = sessionId;
        _configuration = configuration;
        _publish = publish;
        _debug = new DebugController();
        _environment = new SandboxEnvironment(configuration.EnableRobloxMocks);
        _interpreter = new InstrumentedAstInterpreter(_environment, _debug);
        _interpreter.Output += (_, text) => _publish(new SandboxEnvelope(
            SandboxMessageKind.OutputLog,
            SessionId,
            null,
            new OutputLogPayload(SessionId, "stdout", text)));
        _debug.BreakpointHit += (line, path) =>
        {
            State = ExecutionSessionState.Paused;
            _publish(new SandboxEnvelope(
                SandboxMessageKind.BreakpointHit,
                SessionId,
                null,
                new BreakpointHitPayload(SessionId, line, path ?? _sourcePath, "breakpoint")));
        };
    }

    public Guid SessionId { get; }

    public ExecutionSessionState State { get; private set; } = ExecutionSessionState.Created;

    public void LoadScript(string source, string? sourcePath)
    {
        EnsureState(ExecutionSessionState.Created, ExecutionSessionState.Loaded, ExecutionSessionState.Stopped);
        _sourcePath = sourcePath;
        _source = source;
        var snapshot = new SourceSnapshot(SessionId, 1, SourceText.From(source), sourcePath, LuaDialect.Luau);
        _parseResult = new LuaParserService().ParseDocumentAsync(snapshot).GetAwaiter().GetResult();
        State = ExecutionSessionState.Loaded;
    }

    public void SetBreakpoints(string? sourcePath, IReadOnlyList<BreakpointSpec> breakpoints)
    {
        _debug.SetBreakpoints(sourcePath ?? _sourcePath, breakpoints);
    }

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
        _publish(new SandboxEnvelope(
            SandboxMessageKind.SessionStarted,
            SessionId,
            null,
            new SessionStartedPayload(SessionId, State)));

        _executionTask = Task.Run(() => RunExecutionAsync(_sessionCts.Token));
    }

    public void Continue() => _debug.Continue();

    public void Pause() => _debug.RequestPause();

    public void StepOver() => _debug.StepOver();

    public void StepInto() => _debug.StepInto();

    public void StepOut() => _debug.StepOut();

    public void Stop()
    {
        _sessionCts.Cancel();
        State = ExecutionSessionState.Stopped;
        _publish(new SandboxEnvelope(
            SandboxMessageKind.SessionStopped,
            SessionId,
            null,
            new SessionStartedPayload(SessionId, State)));
    }

    public StackTraceResponsePayload GetStackTrace(int frameId)
    {
        var frames = _debug.GetStackFrames(_sourcePath);
        return new StackTraceResponsePayload(SessionId, frames);
    }

    public ScopesResponsePayload GetScopes(int frameId)
    {
        var scopes = _debug.GetScopes(frameId);
        return new ScopesResponsePayload(SessionId, scopes);
    }

    public VariablesResponsePayload GetVariables(int variablesReference)
    {
        var variables = _debug.GetVariables(variablesReference);
        return new VariablesResponsePayload(SessionId, variables);
    }

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
            if (_parseResult?.Tree.Root is not CompilationUnitSyntax unit)
            {
                throw new InvalidOperationException("Script did not produce a valid compilation unit.");
            }

            await _interpreter.ExecuteAsync(unit, _source, _sourcePath, timeoutCts.Token).ConfigureAwait(false);
            Finish(ExecutionFinishReason.Completed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            State = ExecutionSessionState.TimedOut;
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
            _publish(new SandboxEnvelope(
                SandboxMessageKind.ErrorThrown,
                SessionId,
                null,
                new ErrorThrownPayload(SessionId, new ExecutionErrorInfo(
                    ex.Message,
                    _sourcePath,
                    1,
                    1,
                    Array.Empty<string>()))));
            Finish(ExecutionFinishReason.Error);
        }
    }

    private void Finish(ExecutionFinishReason reason)
    {
        _stopwatch?.Stop();
        State = reason == ExecutionFinishReason.Completed
            ? ExecutionSessionState.Stopped
            : State;

        _publish(new SandboxEnvelope(
            SandboxMessageKind.ExecutionFinished,
            SessionId,
            null,
            new ExecutionFinishedPayload(SessionId, reason, _stopwatch?.Elapsed.TotalMilliseconds ?? 0)));
    }

    private void EnsureLoaded()
    {
        if (_parseResult is null)
        {
            throw new InvalidOperationException("LoadScript must be called before execution.");
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
