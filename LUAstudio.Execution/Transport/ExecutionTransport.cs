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

    public async Task<IExecutionHostClient> StartHostAsync(CancellationToken cancellationToken = default)
    {
        if (_client is not null)
        {
            return _client;
        }

        var hostPath = ResolveHostExecutablePath();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"--pipe {SandboxPipeNames.DefaultHostPipe}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            },
            EnableRaisingEvents = true
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start execution host process.");
        }

        var client = new ExecutionHostClient(SandboxPipeNames.DefaultHostPipe);
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _client = client;
        return client;
    }

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        _process?.Dispose();
        _process = null;
    }

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
