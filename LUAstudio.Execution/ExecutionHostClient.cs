using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;
using LUAstudio.Execution.Transport;

namespace LUAstudio.Execution;

public sealed class ExecutionHostClient : IExecutionHostClient
{
    private readonly string _pipeName;
    private NamedPipeSandboxTransport? _transport;
    private readonly ConcurrentQueue<SandboxEnvelope> _events = new();
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;

    public ExecutionHostClient(string pipeName = SandboxPipeNames.DefaultHostPipe)
    {
        _pipeName = pipeName;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            return;
        }

        _transport = await NamedPipeSandboxTransport.ConnectClientAsync(_pipeName, cancellationToken).ConfigureAwait(false);
        _readerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token), CancellationToken.None);
        _connected.TrySetResult();
    }

    public async Task<Guid> CreateSessionAsync(SessionConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();
        await SendCommandAsync(
            SandboxMessageKind.CreateSession,
            sessionId,
            new CreateSessionRequest(sessionId, configuration),
            cancellationToken).ConfigureAwait(false);
        return sessionId;
    }

    public Task LoadScriptAsync(Guid sessionId, string source, string? sourcePath, CancellationToken cancellationToken = default) =>
        SendCommandAsync(
            SandboxMessageKind.LoadScript,
            sessionId,
            new LoadScriptRequest(sessionId, source, sourcePath),
            cancellationToken);

    public Task SetBreakpointsAsync(
        Guid sessionId,
        string? sourcePath,
        IReadOnlyList<BreakpointSpec> breakpoints,
        CancellationToken cancellationToken = default) =>
        SendCommandAsync(
            SandboxMessageKind.SetBreakpoints,
            sessionId,
            new SetBreakpointsRequest(sessionId, sourcePath, breakpoints),
            cancellationToken);

    public Task ExecuteAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.Execute, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task ContinueAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.Continue, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task PauseAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.Pause, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task StepOverAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.StepOver, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task StepIntoAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.StepInto, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task StepOutAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.StepOut, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public Task StopAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        SendCommandAsync(SandboxMessageKind.Stop, sessionId, new SessionCommandRequest(sessionId), cancellationToken);

    public async Task<IReadOnlyList<StackFrameInfo>> GetStackTraceAsync(
        Guid sessionId,
        int frameId = 0,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(
            SandboxMessageKind.StackTrace,
            SandboxMessageKind.StackTraceResponse,
            sessionId,
            new StackTraceRequest(sessionId, frameId),
            cancellationToken).ConfigureAwait(false);

        return SandboxPayload.As<StackTraceResponsePayload>(response?.Payload)?.Frames ?? Array.Empty<StackFrameInfo>();
    }

    public async Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(
        Guid sessionId,
        int frameId,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(
            SandboxMessageKind.Scopes,
            SandboxMessageKind.ScopesResponse,
            sessionId,
            new ScopesRequest(sessionId, frameId),
            cancellationToken).ConfigureAwait(false);

        return SandboxPayload.As<ScopesResponsePayload>(response?.Payload)?.Scopes ?? Array.Empty<ScopeInfo>();
    }

    public async Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(
        Guid sessionId,
        int variablesReference,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(
            SandboxMessageKind.Variables,
            SandboxMessageKind.VariablesResponse,
            sessionId,
            new VariablesRequest(sessionId, variablesReference),
            cancellationToken).ConfigureAwait(false);

        return SandboxPayload.As<VariablesResponsePayload>(response?.Payload)?.Variables ?? Array.Empty<VariableInfo>();
    }

    public async Task<string> EvaluateAsync(
        Guid sessionId,
        int frameId,
        string expression,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(
            SandboxMessageKind.EvaluateExpression,
            SandboxMessageKind.Ack,
            sessionId,
            new EvaluateExpressionRequest(sessionId, frameId, expression),
            cancellationToken).ConfigureAwait(false);

        var payload = SandboxPayload.As<EvaluateResultPayload>(response?.Payload);
        if (!string.IsNullOrWhiteSpace(payload?.Error))
        {
            throw new InvalidOperationException(payload.Error);
        }

        return payload?.Result ?? "nil";
    }

    public async IAsyncEnumerable<SandboxEnvelope> WatchEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _connected.Task.ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_events.TryDequeue(out var evt))
            {
                yield return evt;
                continue;
            }

            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendCommandAsync(
        SandboxMessageKind kind,
        Guid sessionId,
        object payload,
        CancellationToken cancellationToken)
    {
        await EnsureTransportAsync(cancellationToken).ConfigureAwait(false);
        await _transport!.SendAsync(new SandboxEnvelope(kind, sessionId, Guid.NewGuid().ToString("N"), payload), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SandboxEnvelope?> SendRequestAsync(
        SandboxMessageKind requestKind,
        SandboxMessageKind responseKind,
        Guid sessionId,
        object payload,
        CancellationToken cancellationToken)
    {
        await EnsureTransportAsync(cancellationToken).ConfigureAwait(false);
        var requestId = Guid.NewGuid().ToString("N");
        await _transport!.SendAsync(new SandboxEnvelope(requestKind, sessionId, requestId, payload), cancellationToken)
            .ConfigureAwait(false);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var evt in DrainMatching(requestId, responseKind))
            {
                return evt;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for {responseKind}.");
    }

    private IEnumerable<SandboxEnvelope> DrainMatching(string requestId, SandboxMessageKind kind)
    {
        var remaining = new List<SandboxEnvelope>();
        while (_events.TryDequeue(out var evt))
        {
            if (string.Equals(evt.RequestId, requestId, StringComparison.Ordinal) && evt.Kind == kind)
            {
                foreach (var item in remaining)
                {
                    _events.Enqueue(item);
                }

                yield return evt;
                yield break;
            }

            remaining.Add(evt);
        }

        foreach (var item in remaining)
        {
            _events.Enqueue(item);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _transport is not null)
        {
            try
            {
                var envelope = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (envelope is null)
                {
                    break;
                }

                _events.Enqueue(envelope);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EnsureTransportAsync(CancellationToken cancellationToken)
    {
        if (_transport is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _readerCts?.Cancel();
        if (_readerTask is not null)
        {
            try
            {
                await _readerTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore shutdown races.
            }
        }

        if (_transport is not null)
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }

        _readerCts?.Dispose();
    }
}
