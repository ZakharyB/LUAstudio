using LUAstudio.Core.Events;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Events;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Documents;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Text;
using LUAstudio.Workspace;

namespace LUAstudio.IDE.Handlers;

/// <summary>
/// Bridges IDE documents to the IntelliSense analysis pipeline with debounced updates.
/// </summary>
public sealed class DocumentAnalysisHandler
{
    private readonly IDocumentService _documents;
    private readonly IDocumentSnapshotStore _snapshots;
    private readonly IAnalysisOrchestrator _analysis;
    private readonly IWorkspaceService _workspace;
    private readonly IModuleResolver _moduleResolver;
    private readonly Dictionary<Guid, CancellationTokenSource> _debouncers = new();
    private readonly object _debounceLock = new();

    public DocumentAnalysisHandler(
        IDocumentService documents,
        IDocumentSnapshotStore snapshots,
        IAnalysisOrchestrator analysis,
        IWorkspaceService workspace,
        IModuleResolver moduleResolver,
        IEventBus eventBus)
    {
        _documents = documents;
        _snapshots = snapshots;
        _analysis = analysis;
        _workspace = workspace;
        _moduleResolver = moduleResolver;

        eventBus.Subscribe<DocumentOpenedEvent>(OnDocumentOpened);
        eventBus.Subscribe<DocumentClosedEvent>(OnDocumentClosed);
    }

    public void OnDocumentContentChanged(TextDocument document)
    {
        ScheduleAnalysis(document);
    }

    private void OnDocumentOpened(DocumentOpenedEvent e) => ScheduleAnalysis(e.Document);

    private void OnDocumentClosed(DocumentClosedEvent e)
    {
        _snapshots.Remove(e.Document.Id);
        lock (_debounceLock)
        {
            if (_debouncers.Remove(e.Document.Id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    private void ScheduleAnalysis(IDocument document)
    {
        if (document is not TextDocument textDoc)
        {
            return;
        }

        lock (_debounceLock)
        {
            if (_debouncers.TryGetValue(textDoc.Id, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            var cts = new CancellationTokenSource();
            _debouncers[textDoc.Id] = cts;
            _ = DebounceAndAnalyzeAsync(textDoc, cts);
        }
    }

    private async Task DebounceAndAnalyzeAsync(TextDocument document, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(250, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var dialect = DetectDialect(document.FilePath);
        var snapshot = _snapshots.UpdateContent(
            document.Id,
            document.Content,
            document.FilePath,
            dialect);

        _moduleResolver.RebuildIndex(_workspace.Roots.Select(r => r.FullPath));
        _analysis.RequestAnalysis(snapshot);
    }

    private static LuaDialect DetectDialect(string? path) =>
        path?.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) == true ? LuaDialect.Luau : LuaDialect.Lua;
}
