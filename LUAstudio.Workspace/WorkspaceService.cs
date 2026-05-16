using System.Collections.ObjectModel;
using LUAstudio.Core.Events;
using LUAstudio.Core.Logging;
using LUAstudio.Core.Threading;
using LUAstudio.Storage;
using LUAstudio.Workspace.Events;
using LUAstudio.Workspace.FileWatching;

namespace LUAstudio.Workspace;

public interface IWorkspaceService : IDisposable
{
    ObservableCollection<FileSystemEntryNode> RootNodes { get; }

    IReadOnlyList<WorkspaceRoot> Roots { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task AddRootAsync(string folderPath, CancellationToken cancellationToken = default);

    Task RemoveRootAsync(string folderPath, CancellationToken cancellationToken = default);

    Task MoveRootAsync(int fromIndex, int toIndex, CancellationToken cancellationToken = default);

    Task EnsureChildrenLoadedAsync(FileSystemEntryNode node, CancellationToken cancellationToken = default);

    Task SetRestoreWorkspaceRootsAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<bool> GetRestoreWorkspaceRootsAsync(CancellationToken cancellationToken = default);
}

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly IEventBus _eventBus;
    private readonly IAppLogger _logger;
    private readonly IMainThread _mainThread;
    private readonly IWorkspaceRootsRepository _rootsRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly FileSystemWatchCoordinator _watchCoordinator;
    private readonly List<string> _orderedRootPaths = new();
    private readonly Action<WorkspaceFileSystemChangedEvent> _onFileSystemChanged;

    public WorkspaceService(
        IEventBus eventBus,
        IAppLogger logger,
        IMainThread mainThread,
        IWorkspaceRootsRepository rootsRepository,
        ISettingsRepository settingsRepository,
        FileSystemWatchCoordinator watchCoordinator)
    {
        _eventBus = eventBus;
        _logger = logger;
        _mainThread = mainThread;
        _rootsRepository = rootsRepository;
        _settingsRepository = settingsRepository;
        _watchCoordinator = watchCoordinator;
        _onFileSystemChanged = OnWorkspaceFileSystemChanged;
        _eventBus.Subscribe(_onFileSystemChanged);
    }

    public ObservableCollection<FileSystemEntryNode> RootNodes { get; } = new();

    public IReadOnlyList<WorkspaceRoot> Roots =>
        _orderedRootPaths
            .Select((p, i) => new WorkspaceRoot(p, DisplayNameForPath(p), i))
            .ToList();

    public void Dispose()
    {
        _eventBus.Unsubscribe(_onFileSystemChanged);
        foreach (var path in _orderedRootPaths.ToArray())
        {
            _watchCoordinator.UnregisterRoot(path);
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var restore = await GetRestoreWorkspaceRootsAsync(cancellationToken).ConfigureAwait(false);
        _orderedRootPaths.Clear();
        _mainThread.Send(RootNodes.Clear);

        if (!restore)
        {
            _logger.Info("Workspace: restore_workspace_roots disabled; skipping persisted roots.");
            return;
        }

        var records = await _rootsRepository.GetOrderedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var record in records)
        {
            if (!Directory.Exists(record.Path))
            {
                _logger.Warn($"Workspace: skipping missing root '{record.Path}'.");
                continue;
            }

            var normalized = WorkspacePathUtilities.NormalizeDirectory(record.Path);
            if (_orderedRootPaths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _orderedRootPaths.Add(normalized);
        }

        _mainThread.Send(RebuildRootNodes);
        foreach (var path in _orderedRootPaths)
        {
            _watchCoordinator.RegisterRoot(path);
        }

        _eventBus.Publish(new WorkspaceRootsChangedEvent());
    }

    public async Task AddRootAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
        }

        var normalized = WorkspacePathUtilities.NormalizeDirectory(folderPath);
        if (_orderedRootPaths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _orderedRootPaths.Add(normalized);
        await _rootsRepository.ReplaceAllAsync(_orderedRootPaths, cancellationToken).ConfigureAwait(false);
        _watchCoordinator.RegisterRoot(normalized);

        _mainThread.Send(() =>
        {
            var node = new FileSystemEntryNode(normalized, DisplayNameForPath(normalized), isDirectory: true, isWorkspaceRoot: true);
            RootNodes.Add(node);
        });

        _eventBus.Publish(new WorkspaceRootsChangedEvent());
    }

    public async Task RemoveRootAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var normalized = WorkspacePathUtilities.NormalizeDirectory(folderPath);
        var removed = _orderedRootPaths.RemoveAll(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return;
        }

        _watchCoordinator.UnregisterRoot(normalized);
        await _rootsRepository.ReplaceAllAsync(_orderedRootPaths, cancellationToken).ConfigureAwait(false);

        _mainThread.Send(() =>
        {
            for (var i = RootNodes.Count - 1; i >= 0; i--)
            {
                if (string.Equals(RootNodes[i].FullPath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    RootNodes.RemoveAt(i);
                }
            }
        });

        _eventBus.Publish(new WorkspaceRootsChangedEvent());
    }

    public async Task MoveRootAsync(int fromIndex, int toIndex, CancellationToken cancellationToken = default)
    {
        if (fromIndex < 0 || fromIndex >= _orderedRootPaths.Count || toIndex < 0 || toIndex >= _orderedRootPaths.Count || fromIndex == toIndex)
        {
            return;
        }

        var path = _orderedRootPaths[fromIndex];
        _orderedRootPaths.RemoveAt(fromIndex);
        _orderedRootPaths.Insert(toIndex, path);
        await _rootsRepository.ReplaceAllAsync(_orderedRootPaths, cancellationToken).ConfigureAwait(false);

        _mainThread.Send(RebuildRootNodes);
        foreach (var rootPath in _orderedRootPaths)
        {
            _watchCoordinator.RegisterRoot(rootPath);
        }

        _eventBus.Publish(new WorkspaceRootsChangedEvent());
    }

    public async Task EnsureChildrenLoadedAsync(FileSystemEntryNode node, CancellationToken cancellationToken = default)
    {
        if (!node.IsDirectory || node.IsTruncationPlaceholder || node.IsChildrenLoaded)
        {
            return;
        }

        try
        {
            var (slice, truncated) = await Task.Run(() =>
            {
                var entries = new List<(string Path, bool IsDirectory)>();
                foreach (var entry in Directory.EnumerateFileSystemEntries(node.FullPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attr = File.GetAttributes(entry);
                    var isDir = attr.HasFlag(FileAttributes.Directory);
                    entries.Add((entry, isDir));
                }

                entries.Sort((a, b) =>
                {
                    if (a.IsDirectory != b.IsDirectory)
                    {
                        return a.IsDirectory ? -1 : 1;
                    }

                    return string.Compare(Path.GetFileName(a.Path), Path.GetFileName(b.Path), StringComparison.OrdinalIgnoreCase);
                });

                var isTruncated = entries.Count > FileSystemEntryNode.MaxChildrenPerDirectory;
                var take = isTruncated ? FileSystemEntryNode.MaxChildrenPerDirectory - 1 : entries.Count;
                return (Entries: entries.Take(take).ToList(), Truncated: isTruncated);
            }, cancellationToken).ConfigureAwait(false);

            _mainThread.Send(() =>
            {
                node.Children.Clear();
                foreach (var (path, isDir) in slice)
                {
                    var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(name))
                    {
                        name = path;
                    }

                    node.Children.Add(new FileSystemEntryNode(path, name, isDir, isWorkspaceRoot: false));
                }

                if (truncated)
                {
                    node.Children.Add(FileSystemEntryNode.CreateTruncationNotice(node.FullPath));
                }

                node.LoadError = null;
                node.IsChildrenLoaded = true;
            });
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to enumerate '{node.FullPath}': {ex.Message}");
            _mainThread.Send(() =>
            {
                node.Children.Clear();
                node.LoadError = ex.Message;
                node.IsChildrenLoaded = true;
            });
        }
    }

    public Task SetRestoreWorkspaceRootsAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        return _settingsRepository.SetAsync(
            WorkspaceSettingsKeys.RestoreWorkspaceRoots,
            enabled ? "true" : "false",
            cancellationToken);
    }

    public async Task<bool> GetRestoreWorkspaceRootsAsync(CancellationToken cancellationToken = default)
    {
        var v = await _settingsRepository.GetAsync(WorkspaceSettingsKeys.RestoreWorkspaceRoots, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v))
        {
            return true;
        }

        return !string.Equals(v.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildRootNodes()
    {
        RootNodes.Clear();
        foreach (var path in _orderedRootPaths)
        {
            RootNodes.Add(new FileSystemEntryNode(path, DisplayNameForPath(path), isDirectory: true, isWorkspaceRoot: true));
        }
    }

    private static string DisplayNameForPath(string fullPath)
    {
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private void OnWorkspaceFileSystemChanged(WorkspaceFileSystemChangedEvent e)
    {
        _mainThread.Send(() =>
        {
            foreach (var path in e.AffectedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                InvalidatePath(path);
            }
        });
    }

    private void InvalidatePath(string anyPath)
    {
        string full;
        try
        {
            full = Path.GetFullPath(anyPath);
        }
        catch
        {
            return;
        }

        string? targetDir;
        if (File.Exists(full))
        {
            targetDir = Path.GetDirectoryName(full);
        }
        else if (Directory.Exists(full))
        {
            targetDir = full;
        }
        else
        {
            targetDir = Path.GetDirectoryName(full);
        }

        if (string.IsNullOrEmpty(targetDir))
        {
            return;
        }

        var cursor = targetDir;
        while (!string.IsNullOrEmpty(cursor))
        {
            var node = FindFolderNodeInTree(cursor);
            if (node is not null)
            {
                ClearLoadedChildren(node);
                return;
            }

            cursor = Path.GetDirectoryName(cursor)!;
        }
    }

    private FileSystemEntryNode? FindFolderNodeInTree(string directoryFullPath)
    {
        foreach (var root in RootNodes)
        {
            var found = FindFolderRecursive(root, directoryFullPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static FileSystemEntryNode? FindFolderRecursive(FileSystemEntryNode node, string directoryFullPath)
    {
        if (string.Equals(node.FullPath, directoryFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        if (!node.IsChildrenLoaded)
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            if (!child.IsDirectory || child.IsTruncationPlaceholder)
            {
                continue;
            }

            var found = FindFolderRecursive(child, directoryFullPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ClearLoadedChildren(FileSystemEntryNode node)
    {
        node.Children.Clear();
        node.IsChildrenLoaded = false;
        node.LoadError = null;
    }
}
