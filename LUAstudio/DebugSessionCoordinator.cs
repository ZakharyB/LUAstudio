using LUAstudio.Editor.Debugging;
using LUAstudio.Execution.Abstractions;

namespace LUAstudio;

public sealed class DebugSessionCoordinator : IAsyncDisposable
{
    private readonly IExecutionHostProcessManager _hostManager;
    private readonly IBreakpointService _breakpoints;
    private readonly WorkspaceModuleBridge _moduleBridge;
    private IExecutionHostClient? _client;
    private Guid _sessionId;

    public DebugSessionCoordinator(
        IExecutionHostProcessManager hostManager,
        IBreakpointService breakpoints,
        WorkspaceModuleBridge moduleBridge)
    {
        _hostManager = hostManager;
        _hostManager.Log += OnHostLog;
        _breakpoints = breakpoints;
        _moduleBridge = moduleBridge;
    }

    public IExecutionHostClient? Client => _client;

    public event EventHandler<ExecutionHostLogEventArgs>? HostLog;

    private void OnHostLog(object? sender, ExecutionHostLogEventArgs e) => HostLog?.Invoke(this, e);

    public async Task<IExecutionHostClient> EnsureHostRunningAsync(CancellationToken cancellationToken = default)
    {
        _client ??= await _hostManager.StartHostAsync(cancellationToken).ConfigureAwait(false);
        return _client;
    }

    public async Task<Guid> RunAsync(
        string source,
        string? sourcePath,
        SessionConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        _sessionId = Guid.Empty;
        WriteLog($"Run requested for {sourcePath ?? "<untitled>"} ({source.Length} source characters).");
        var client = await EnsureHostRunningAsync(cancellationToken).ConfigureAwait(false);
        _sessionId = await client.CreateSessionAsync(configuration ?? new SessionConfiguration(), cancellationToken)
            .ConfigureAwait(false);
        WriteLog($"Sandbox session created: {_sessionId}.");

        var modules = _moduleBridge.CreateSnapshot(sourcePath, source);
        await client.SetWorkspaceModulesAsync(_sessionId, modules, cancellationToken).ConfigureAwait(false);
        WriteLog($"Workspace snapshot sent: {modules.Count} module(s).");

        var breakpointCount = 0;
        foreach (var (path, breakpoints) in _breakpoints.GetBreakpointGroups())
        {
            await client.SetBreakpointsAsync(_sessionId, path, breakpoints, cancellationToken).ConfigureAwait(false);
            breakpointCount += breakpoints.Count;
        }
        WriteLog($"Breakpoints sent: {breakpointCount}.");

        await client.LoadScriptAsync(_sessionId, source, sourcePath, cancellationToken).ConfigureAwait(false);
        WriteLog("Script loaded by sandbox VM.");
        await client.ExecuteAsync(_sessionId, cancellationToken).ConfigureAwait(false);
        WriteLog("Execute command accepted; sandbox VM is running.");
        return _sessionId;
    }

    public Guid CurrentSessionId => _sessionId;

    private void WriteLog(string message) => HostLog?.Invoke(this, new ExecutionHostLogEventArgs(message));

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
    {
        await _hostManager.StopHostAsync(cancellationToken).ConfigureAwait(false);
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        _hostManager.Log -= OnHostLog;
        await StopHostAsync().ConfigureAwait(false);
    }
}
