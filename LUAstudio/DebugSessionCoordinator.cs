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
        _breakpoints = breakpoints;
        _moduleBridge = moduleBridge;
    }

    public IExecutionHostClient? Client => _client;

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
        var client = await EnsureHostRunningAsync(cancellationToken).ConfigureAwait(false);
        _sessionId = await client.CreateSessionAsync(configuration ?? new SessionConfiguration(), cancellationToken)
            .ConfigureAwait(false);

        var modules = _moduleBridge.CreateSnapshot(sourcePath, source);
        await client.SetWorkspaceModulesAsync(_sessionId, modules, cancellationToken).ConfigureAwait(false);

        foreach (var (path, breakpoints) in _breakpoints.GetBreakpointGroups())
        {
            await client.SetBreakpointsAsync(_sessionId, path, breakpoints, cancellationToken).ConfigureAwait(false);
        }

        await client.LoadScriptAsync(_sessionId, source, sourcePath, cancellationToken).ConfigureAwait(false);
        await client.ExecuteAsync(_sessionId, cancellationToken).ConfigureAwait(false);
        return _sessionId;
    }

    public Guid CurrentSessionId => _sessionId;

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
    {
        await _hostManager.StopHostAsync(cancellationToken).ConfigureAwait(false);
        _client = null;
    }

    public async ValueTask DisposeAsync() => await StopHostAsync().ConfigureAwait(false);
}
