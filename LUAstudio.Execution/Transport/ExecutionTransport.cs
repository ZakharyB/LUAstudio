using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio.Execution.Transport;

public sealed class NamedPipeSandboxTransport : IAsyncDisposable
{
    private static readonly Encoding PipeEncoding =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private NamedPipeSandboxTransport(StreamReader reader, StreamWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public static async Task<NamedPipeSandboxTransport> ConnectClientAsync(
        string pipeName,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        log?.Invoke("[PIPE] Constructing client");

        var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            log?.Invoke("[PIPE] BEFORE ConnectAsync");

            await client
                .ConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            log?.Invoke("[PIPE] AFTER ConnectAsync");
            log?.Invoke("[PIPE] BEFORE FromStream");

            var transport = FromStream(client);

            log?.Invoke("[PIPE] AFTER FromStream");

            return transport;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[PIPE] ERROR: {ex}");

            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static NamedPipeSandboxTransport FromStream(Stream stream)
    {
        var reader = new StreamReader(
            stream,
            PipeEncoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        var writer = new StreamWriter(
            stream,
            PipeEncoding,
            bufferSize: 4096,
            leaveOpen: true);

        // IMPORTANT:
        // Do not enable AutoFlush here.
        // AutoFlush performs an immediate flush while the transport
        // is still being constructed.

        return new NamedPipeSandboxTransport(
            reader,
            writer);
    }


    public async Task SendAsync(
        SandboxEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        await _writeLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var json = SandboxJson.Serialize(envelope);

            await _writer
                .WriteLineAsync(
                    json.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);

            // Flush AFTER an actual message has been written.
            // By this point both sides' transports/read loops exist.
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
        CancellationToken cancellationToken = default)
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

public sealed class ExecutionHostProcessManager : IExecutionHostProcessManager
{
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private Process? _process;
    private IExecutionHostClient? _client;

    public event EventHandler<ExecutionHostLogEventArgs>? Log;

public async Task<IExecutionHostClient> StartHostAsync(
    CancellationToken cancellationToken = default)
{
    WriteLog("[MANAGER] StartHostAsync ENTER");

    if (_client is not null)
    {
        WriteLog("[MANAGER] Existing client returned.");
        return _client;
    }

    WriteLog("[MANAGER] BEFORE startup lock");

    await _startLock
        .WaitAsync(cancellationToken)
        .ConfigureAwait(false);

    WriteLog("[MANAGER] AFTER startup lock");

    try
    {
        if (_client is not null)
        {
            WriteLog("[MANAGER] Client created by another caller.");
            return _client;
        }

        var hostPath = ResolveHostExecutablePath();
        var pipeName = SandboxPipeNames.ForIdeProcess(
            Environment.ProcessId);

        WriteLog($"[MANAGER] Pipe = {pipeName}");
        WriteLog($"[MANAGER] Host = {hostPath}");

        if (_process is null || _process.HasExited)
        {
            _process?.Dispose();

            var startInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(hostPath)!
            };

            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(pipeName);

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    WriteLog($"HOST OUT: {e.Data}");
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    WriteLog($"HOST ERR: {e.Data}");
            };

            process.Exited += (_, _) =>
            {
                try
                {
                    WriteLog(
                        $"[MANAGER] Host exited: {process.ExitCode}");
                }
                catch
                {
                }
            };

            WriteLog("[MANAGER] BEFORE process.Start");

            if (!process.Start())
            {
                process.Dispose();

                throw new InvalidOperationException(
                    "Failed to start execution host.");
            }

            _process = process;

            WriteLog(
                $"[MANAGER] Process started PID={process.Id}");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

       // var client = new ExecutionHostClient(pipeName);
        var client = new ExecutionHostClient(
            pipeName,
            message => WriteLog(message));
        
        WriteLog("[MANAGER] BEFORE client.ConnectAsync");

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await client
                .ConnectAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            WriteLog(
                "[MANAGER] client.ConnectAsync TIMED OUT");

            await client.DisposeAsync();

            throw new TimeoutException(
                $"Could not connect to execution host pipe '{pipeName}'.");
        }

        WriteLog("[MANAGER] AFTER client.ConnectAsync");

        _client = client;

        WriteLog("[MANAGER] _client assigned");
        WriteLog("[MANAGER] StartHostAsync EXIT");

        return client;
    }
    finally
    {
        WriteLog("[MANAGER] Releasing startup lock");
        _startLock.Release();
    }
}

    public async Task StopHostAsync(
        CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            WriteLog("Execution host shutdown requested.");

            if (_client is not null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;

                WriteLog("Named-pipe client disconnected.");
            }

            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);

                await _process
                    .WaitForExitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            _process?.Dispose();
            _process = null;

            WriteLog("Execution host shutdown complete.");
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task KillHostProcessAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);

                await _process
                    .WaitForExitAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private void WriteLog(string message) =>
        Log?.Invoke(
            this,
            new ExecutionHostLogEventArgs(message));

    private static string ResolveHostExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;

        var candidate = Path.Combine(
            baseDir,
            "LUAstudio.ExecutionHost.exe");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(
            baseDir,
            "LUAstudio.ExecutionHost");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException(
            "Execution host executable was not found next to the IDE. " +
            "Build LUAstudio.ExecutionHost.");
    }
}