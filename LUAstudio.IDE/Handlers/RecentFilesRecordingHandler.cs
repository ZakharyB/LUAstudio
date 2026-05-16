using LUAstudio.Core.Events;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Events;
using LUAstudio.Workspace;

namespace LUAstudio.IDE.Handlers;

public sealed class RecentFilesRecordingHandler : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IRecentFilesService _recentFiles;
    private readonly Action<DocumentOpenedEvent> _onOpened;

    public RecentFilesRecordingHandler(IEventBus eventBus, IRecentFilesService recentFiles)
    {
        _eventBus = eventBus;
        _recentFiles = recentFiles;
        _onOpened = OnDocumentOpened;
        _eventBus.Subscribe(_onOpened);
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe(_onOpened);
    }

    private void OnDocumentOpened(DocumentOpenedEvent e)
    {
        if (e.Document is not TextDocument doc || doc.FilePath is null)
        {
            return;
        }

        _ = _recentFiles.RecordFileOpenedAsync(doc.FilePath);
    }
}
