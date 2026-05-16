using LUAstudio.Core.Events;
using LUAstudio.Core.Threading;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Services;
using LUAstudio.Workspace.Events;

namespace LUAstudio.IDE.Handlers;

public sealed class DocumentSyncHandler : IDisposable
{
    private sealed record RemovedFileSnapshot(string DisplayName, bool IsDirty);

    private sealed record OpenFileSnapshot(TextDocument Document, bool IsDirty, string DisplayName);

    private readonly IEventBus _eventBus;
    private readonly IDocumentService _documents;
    private readonly IFileSystemActivitySink _activitySink;
    private readonly IMainThread _mainThread;
    private readonly Action<WorkspaceFileSystemChangedEvent> _onFs;

    public DocumentSyncHandler(
        IEventBus eventBus,
        IDocumentService documents,
        IFileSystemActivitySink activitySink,
        IMainThread mainThread)
    {
        _eventBus = eventBus;
        _documents = documents;
        _activitySink = activitySink;
        _mainThread = mainThread;
        _onFs = OnWorkspaceFileSystemChanged;
        _eventBus.Subscribe(_onFs);
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe(_onFs);
    }

    private void OnWorkspaceFileSystemChanged(WorkspaceFileSystemChangedEvent e)
    {
        _ = Task.Run(() => HandleAsync(e));
    }

    private async Task HandleAsync(WorkspaceFileSystemChangedEvent e)
    {
        foreach (var raw in e.AffectedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string full;
            try
            {
                full = Path.GetFullPath(raw);
            }
            catch
            {
                continue;
            }

            if (!File.Exists(full))
            {
                if (Directory.Exists(full))
                {
                    continue;
                }

                var removedInfo = _mainThread.Invoke<RemovedFileSnapshot?>(() =>
                {
                    var doc = _documents.Documents.FirstOrDefault(d =>
                        d.FilePath is not null &&
                        string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));

                    return doc is null ? null : new RemovedFileSnapshot(doc.DisplayName, doc.IsDirty);
                });

                if (removedInfo is not null)
                {
                    _activitySink.ReportFileSystemActivity(
                        removedInfo.IsDirty
                            ? $"{removedInfo.DisplayName}: file removed or moved on disk (unsaved buffer)."
                            : $"{removedInfo.DisplayName}: file removed or moved on disk.");
                }

                continue;
            }

            var snapshot = _mainThread.Invoke<OpenFileSnapshot?>(() =>
            {
                var doc = _documents.Documents.FirstOrDefault(d =>
                    d.FilePath is not null &&
                    string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));

                return doc is null ? null : new OpenFileSnapshot(doc, doc.IsDirty, doc.DisplayName);
            });

            if (snapshot is null)
            {
                continue;
            }

            if (snapshot.IsDirty)
            {
                _activitySink.ReportFileSystemActivity($"{snapshot.DisplayName}: changed on disk (unsaved buffer).");
                continue;
            }

            try
            {
                await _documents.ReloadFromDiskAsync(snapshot.Document).ConfigureAwait(false);
                _activitySink.ReportFileSystemActivity(null);
            }
            catch (Exception)
            {
                _activitySink.ReportFileSystemActivity($"{snapshot.DisplayName}: failed to reload from disk.");
            }
        }
    }
}
