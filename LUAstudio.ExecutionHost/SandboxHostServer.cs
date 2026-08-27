using System.IO.Pipes;
using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;
using LUAstudio.ExecutionHost.Sessions;

namespace LUAstudio.ExecutionHost;

public sealed class SandboxHostServer
{
    private readonly SessionManager _sessions = new();
    private readonly string _pipeName;

    public SandboxHostServer(string pipeName) => _pipeName = pipeName;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SERVER] RunAsync entered for '{_pipeName}'");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[SERVER] Creating NamedPipeServerStream '{_pipeName}'");

            var stream = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            Console.WriteLine($"[SERVER] Pipe created. Waiting for client '{_pipeName}'");

            try
            {
                await stream
                    .WaitForConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);

                Console.WriteLine($"[SERVER] CLIENT CONNECTED '{_pipeName}'");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[SERVER] WaitForConnectionAsync failed: {ex}");

                stream.Dispose();
                throw;
            }

            _ = Task.Run(
                () => HandleClientAsync(stream, cancellationToken),
                CancellationToken.None);
        }

        Console.WriteLine("[SERVER] RunAsync exiting");
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        var transport = new PipeTransport(stream);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (envelope is null)
                {
                    break;
                }

                var response = await DispatchAsync(envelope, transport, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                {
                    await transport.SendAsync(response, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            if (stream is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                stream.Dispose();
            }
        }
    }

    private async Task<SandboxEnvelope?> DispatchAsync(
        SandboxEnvelope envelope,
        PipeTransport transport,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine(
                $"[SERVER] Received {envelope.Kind} " +
                $"Session={envelope.SessionId} " +
                $"Request={envelope.RequestId}");
            switch (envelope.Kind)
            {
                case SandboxMessageKind.CreateSession:
                {
                    var request = SandboxPayload.As<CreateSessionRequest>(envelope.Payload)
                        ?? throw new InvalidOperationException("Missing CreateSession payload.");
                    _sessions.Create(
                        request.SessionId,
                        request.Configuration,
                        evt => _ = transport.SendAsync(evt, cancellationToken));

                    await transport.SendAsync(
                        new SandboxEnvelope(
                            SandboxMessageKind.ExecutionStateChanged,
                            request.SessionId,
                            null,
                            new ExecutionStateChangedPayload(
                                request.SessionId,
                                ExecutionSessionState.Created)),
                        cancellationToken);

                    return Ack(envelope, null);
                }

                case SandboxMessageKind.LoadScript:
                {
                    var request = Required<LoadScriptRequest>(envelope);

                    _sessions
                        .Get(request.SessionId)
                        .LoadScript(
                            request.Source,
                            request.SourcePath);

                    await transport.SendAsync(
                        new SandboxEnvelope(
                            SandboxMessageKind.ExecutionStateChanged,
                            request.SessionId,
                            null,
                            new ExecutionStateChangedPayload(
                                request.SessionId,
                                ExecutionSessionState.Loaded)),
                        cancellationToken);

                    return Ack(envelope, null);
                }

                case SandboxMessageKind.SetBreakpoints:
                {
                    var request = Required<SetBreakpointsRequest>(envelope);
                    _sessions.Get(request.SessionId).SetBreakpoints(request.SourcePath, request.Breakpoints);
                    return Ack(envelope, null);
                }

                case SandboxMessageKind.SetWorkspaceModules:
                {
                    var request = Required<SetWorkspaceModulesRequest>(envelope);
                    _sessions.Get(request.SessionId).SetWorkspaceModules(request.Modules);
                    return Ack(envelope, null);
                }

                case SandboxMessageKind.LoadModule:
                {
                    var request = Required<LoadModuleRequest>(envelope);
                    _sessions.Get(request.SessionId).LoadModule(request.Path, request.Source);
                    return Ack(envelope, null);
                }

                case SandboxMessageKind.ConfigureEnvironment:
                {
                    var request = Required<ConfigureEnvironmentRequest>(envelope);
                    _sessions.Get(request.SessionId).ConfigureEnvironment(
                        request.EnvironmentProfile,
                        request.EnableRobloxMocks,
                        request.AllowNetwork);
                    return Ack(envelope, null);
                }

                case SandboxMessageKind.Execute:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).Execute();
                    return Ack(envelope, null);

                case SandboxMessageKind.Continue:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).Continue();
                    return Ack(envelope, null);

                case SandboxMessageKind.Pause:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).Pause();
                    return Ack(envelope, null);

                case SandboxMessageKind.StepOver:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).StepOver();
                    return Ack(envelope, null);

                case SandboxMessageKind.StepInto:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).StepInto();
                    return Ack(envelope, null);

                case SandboxMessageKind.StepOut:
                    _sessions.Get(Required<SessionCommandRequest>(envelope).SessionId).StepOut();
                    return Ack(envelope, null);

                case SandboxMessageKind.Stop:
                    _sessions.Stop(Required<SessionCommandRequest>(envelope).SessionId);
                    return Ack(envelope, null);

                case SandboxMessageKind.StackTrace:
                {
                    var request = Required<StackTraceRequest>(envelope);
                    var payload = _sessions.Get(request.SessionId).GetStackTrace(request.FrameId);
                    return new SandboxEnvelope(SandboxMessageKind.StackTraceResponse, request.SessionId, envelope.RequestId, payload);
                }

                case SandboxMessageKind.Scopes:
                {
                    var request = Required<ScopesRequest>(envelope);
                    var payload = _sessions.Get(request.SessionId).GetScopes(request.FrameId);
                    return new SandboxEnvelope(SandboxMessageKind.ScopesResponse, request.SessionId, envelope.RequestId, payload);
                }

                case SandboxMessageKind.Variables:
                {
                    var request = Required<VariablesRequest>(envelope);
                    var payload = _sessions.Get(request.SessionId).GetVariables(request.VariablesReference);
                    return new SandboxEnvelope(SandboxMessageKind.VariablesResponse, request.SessionId, envelope.RequestId, payload);
                }

                case SandboxMessageKind.EvaluateExpression:
                {
                    var request = Required<EvaluateExpressionRequest>(envelope);
                    var payload = _sessions.Get(request.SessionId).Evaluate(request.FrameId, request.Expression);
                    return Ack(envelope, payload);
                }

                default:
                    return Error(envelope, $"Unsupported message kind '{envelope.Kind}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[SERVER] Dispatch error for {envelope.Kind}: {ex}");
            return Error(envelope, ex.Message);
        }
    }

    private static T Required<T>(SandboxEnvelope envelope) =>
        SandboxPayload.As<T>(envelope.Payload) ?? throw new InvalidOperationException($"Missing payload for {typeof(T).Name}.");

    private static SandboxEnvelope Ack(SandboxEnvelope request, object? payload) =>
        new(SandboxMessageKind.Ack, request.SessionId, request.RequestId, payload);

    private static SandboxEnvelope Error(SandboxEnvelope request, string message) =>
        new(SandboxMessageKind.Error, request.SessionId, request.RequestId, new { message });

    private sealed class PipeTransport : IAsyncDisposable
    {
        private static readonly Encoding PipeEncoding =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public PipeTransport(Stream stream)
        {
            _reader = new StreamReader(
                stream,
                PipeEncoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            _writer = new StreamWriter(
                stream,
                PipeEncoding,
                bufferSize: 4096,
                leaveOpen: true);

            // NO AutoFlush here.
        }

        public async Task SendAsync(
            SandboxEnvelope envelope,
            CancellationToken cancellationToken)
        {
            await _writeLock
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _writer
                    .WriteLineAsync(
                        SandboxJson.Serialize(envelope).AsMemory(),
                        cancellationToken)
                    .ConfigureAwait(false);

                await _writer
                    .FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<SandboxEnvelope?> ReceiveAsync(
            CancellationToken cancellationToken)
        {
            var line = await _reader
                .ReadLineAsync(cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(line)
                ? null
                : SandboxJson.Deserialize(line);
        }

        public async ValueTask DisposeAsync()
        {
            _writeLock.Dispose();
            _reader.Dispose();

            await _writer
                .DisposeAsync()
                .ConfigureAwait(false);
        }
    }
}
