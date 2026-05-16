using LUAstudio.Core.Events;
using LUAstudio.Core.Logging;
using LUAstudio.Workspace.Events;

namespace LUAstudio.Workspace.FileWatching;

public sealed class FileSystemWatchCoordinator : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(280);

    private readonly IEventBus _eventBus;
    private readonly IAppLogger _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, RootWatch> _roots = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemWatchCoordinator(IEventBus eventBus, IAppLogger logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public void RegisterRoot(string rootFullPath)
    {
        var root = WorkspacePathUtilities.NormalizeDirectory(rootFullPath);
        lock (_lock)
        {
            if (_roots.ContainsKey(root))
            {
                return;
            }

            _roots[root] = new RootWatch(root, DebounceDelay, Publish, _logger);
        }
    }

    public void UnregisterRoot(string rootFullPath)
    {
        var root = WorkspacePathUtilities.NormalizeDirectory(rootFullPath);
        lock (_lock)
        {
            if (_roots.Remove(root, out var watch))
            {
                watch.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var w in _roots.Values)
            {
                w.Dispose();
            }

            _roots.Clear();
        }
    }

    private void Publish(string root, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        _eventBus.Publish(new WorkspaceFileSystemChangedEvent(root, paths));
    }

    private sealed class RootWatch : IDisposable
    {
        private readonly string _root;
        private readonly TimeSpan _delay;
        private readonly Action<string, IReadOnlyList<string>> _publish;
        private readonly IAppLogger _logger;
        private readonly FileSystemWatcher _watcher;
        private readonly object _gate = new();
        private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _debounceCts;

        public RootWatch(string root, TimeSpan delay, Action<string, IReadOnlyList<string>> publish, IAppLogger logger)
        {
            _root = root;
            _delay = delay;
            _publish = publish;
            _logger = logger;
            _watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size,
            };

            _watcher.Created += OnFsEvent;
            _watcher.Deleted += OnFsEvent;
            _watcher.Changed += OnFsEvent;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _logger.Warn($"File watcher error for '{_root}': {e.GetException().Message}");
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Enqueue(e.OldFullPath);
            Enqueue(e.FullPath);
        }

        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            Enqueue(e.FullPath);
        }

        private void Enqueue(string fullPath)
        {
            lock (_gate)
            {
                _pending.Add(Path.GetFullPath(fullPath));
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;
                _ = DebounceFlushAsync(token);
            }
        }

        private async Task DebounceFlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            string[] snapshot;
            lock (_gate)
            {
                snapshot = _pending.ToArray();
                _pending.Clear();
            }

            if (snapshot.Length > 0)
            {
                _publish(_root, snapshot);
            }
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            lock (_gate)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
        }
    }
}
