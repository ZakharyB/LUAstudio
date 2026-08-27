using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio.Execution.Transport;

public sealed class NamedPipeSandboxTransport : IAsyncDisposable
{
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
        CancellationToken cancellationToken = default)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return FromStream(client);
    }

    public static NamedPipeSandboxTransport FromStream(Stream stream)
    {
        var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        return new NamedPipeSandboxTransport(reader, writer);
    }

    public async Task SendAsync(SandboxEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = SandboxJson.Serialize(envelope);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<SandboxEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(line) ? null : SandboxJson.Deserialize(line);
    }

    public async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        _reader.Dispose();
        await _writer.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class ExecutionHostProcessManager : IExecutionHostProcessManager
{
    private Process? _process;
    private IExecutionHostClient? _client;

    public event EventHandler<ExecutionHostLogEventArgs>? Log;

    public async Task<IExecutionHostClient> StartHostAsync(CancellationToken cancellationToken = default)
    {
        if (_client is not null)
        {
            WriteLog("Start requested; the execution host is already connected.");
            return _client;
        }

        var hostPath = ResolveHostExecutablePath();
        var idePid = Environment.ProcessId;
        var pipeName = SandboxPipeNames.ForIdeProcess(idePid);
        WriteLog($"LUAstudio.exe PID {idePid}");
        WriteLog($"Allocated private pipe: {pipeName}");
        WriteLog($"Resolved host executable: {hostPath}");
        WriteLog($"Launching: {Path.GetFileName(hostPath)} --pipe {pipeName}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                ArgumentList = { "--pipe", pipeName },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) WriteLog($"stdout: {e.Data}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) WriteLog($"stderr: {e.Data}");
        };
        process.Exited += (_, _) => WriteLog($"Process exited with code {process.ExitCode}.");
        _process = process;

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start execution host process.");
        }

        WriteLog($"Process started: LUAstudio.ExecutionHost PID {process.Id}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        WriteLog($"Connecting named-pipe client to {pipeName}...");
        var client = new ExecutionHostClient(pipeName);
        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _client = client;
            WriteLog("Named-pipe connection established; sandbox host is ready.");
            return client;
        }
        catch (Exception ex)
        {
            WriteLog($"Launch failed while connecting to {pipeName}: {ex.Message}");
            await client.DisposeAsync().ConfigureAwait(false);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            process.Dispose();
            _process = null;
            throw;
        }
    }

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
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
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        _process?.Dispose();
        _process = null;
        WriteLog("Execution host shutdown complete.");
    }

    private void WriteLog(string message) => Log?.Invoke(this, new ExecutionHostLogEventArgs(message));

    private static string ResolveHostExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "LUAstudio.ExecutionHost.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(baseDir, "LUAstudio.ExecutionHost");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException(
            "Execution host executable was not found next to the IDE. Build LUAstudio.ExecutionHost.");
    }
}
