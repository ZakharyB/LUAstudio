using LUAstudio.Core.Events;
using LUAstudio.Workspace;
using LUAstudio.Workspace.Events;

namespace LUAstudio.IntelliSense.Workspace;

/// <summary>
/// Keeps the workspace require graph in sync when roots or files change.
/// </summary>
public sealed class RequireGraphCoordinator : IDisposable
{
    private readonly IWorkspaceService _workspace;
    private readonly IModuleResolver _moduleResolver;
    private readonly RequireGraphWorkspaceScanner _scanner;
    private readonly object _scanLock = new();
    private CancellationTokenSource? _scanCts;

    public RequireGraphCoordinator(
        IEventBus eventBus,
        IWorkspaceService workspace,
        IModuleResolver moduleResolver,
        RequireGraphWorkspaceScanner scanner)
    {
        _workspace = workspace;
        _moduleResolver = moduleResolver;
        _scanner = scanner;

        eventBus.Subscribe<WorkspaceRootsChangedEvent>(_ => ScheduleScan());
        eventBus.Subscribe<WorkspaceFileSystemChangedEvent>(_ => ScheduleScan());
    }

    public void ScheduleScan()
    {
        lock (_scanLock)
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;
            _ = ScanDebouncedAsync(token);
        }
    }

    private async Task ScanDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var roots = _workspace.Roots.Select(r => r.FullPath).ToArray();
        _moduleResolver.RebuildIndex(roots);
        await _scanner.ScanAsync(roots, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_scanLock)
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }
}
