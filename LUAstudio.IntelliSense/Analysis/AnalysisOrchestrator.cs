using System.Collections.Concurrent;
using System.Diagnostics;
using LUAstudio.Core.Events;
using LUAstudio.Core.Threading;
using LUAstudio.IntelliSense.Events;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Analysis;

public sealed class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly ILuaParser _parser;
    private readonly SemanticAnalyzer _semanticAnalyzer;
    private readonly ISymbolIndex _symbolIndex;
    private readonly IAnalysisWorkQueue _workQueue;
    private readonly IEventBus _eventBus;
    private readonly RequireGraphWorkspaceScanner _requireGraphScanner;
    private readonly ConcurrentDictionary<Guid, DocumentAnalysisResult> _latest = new();
    private readonly ConcurrentDictionary<Guid, ParseResult> _previousParse = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastRequestTime = new();
    private DateTime _lastRequireGraphPublishUtc = DateTime.MinValue;
    private const int DebounceMs = 0;
    private const int RequireGraphThrottleMs = 2000;

    public AnalysisOrchestrator(
        ILuaParser parser,
        SemanticAnalyzer semanticAnalyzer,
        ISymbolIndex symbolIndex,
        IAnalysisWorkQueue workQueue,
        IEventBus eventBus,
        RequireGraphWorkspaceScanner requireGraphScanner)
    {
        _parser = parser;
        _semanticAnalyzer = semanticAnalyzer;
        _symbolIndex = symbolIndex;
        _workQueue = workQueue;
        _eventBus = eventBus;
        _requireGraphScanner = requireGraphScanner;
    }

    public DocumentAnalysisResult? GetLatestResult(Guid documentId) =>
        _latest.TryGetValue(documentId, out var result) ? result : null;

    public void RequestAnalysis(SourceSnapshot snapshot, TextSpan? changedSpan = null)
    {
        var key = snapshot.DocumentId.ToString();
        var now = DateTime.UtcNow;
        _lastRequestTime[snapshot.DocumentId] = now;

        _workQueue.CancelPending(key);
        _ = _workQueue.EnqueueAsync(key, async ct =>
        {
            await Task.Delay(DebounceMs, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var lastRequest = _lastRequestTime.TryGetValue(snapshot.DocumentId, out var t) ? t : DateTime.MinValue;
            if (now != lastRequest)
            {
                return;
            }

            await RunAnalysisAsync(snapshot, changedSpan, ct).ConfigureAwait(false);
        });
    }

    public async Task<DocumentAnalysisResult> AnalyzeAsync(
        SourceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        return await RunAnalysisAsync(snapshot, changedSpan: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentAnalysisResult> RunAnalysisAsync(
        SourceSnapshot snapshot,
        TextSpan? changedSpan,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        IncrementalParseContext? incremental = null;
        if (changedSpan is not null &&
            _previousParse.TryGetValue(snapshot.DocumentId, out var prev))
        {
            incremental = new IncrementalParseContext(prev, changedSpan.Value);
        }

        var parseResult = await _parser.ParseDocumentAsync(snapshot, incremental, cancellationToken).ConfigureAwait(false);
        _previousParse[snapshot.DocumentId] = parseResult;

        var semantic = _semanticAnalyzer.Analyze(parseResult);
        _symbolIndex.UpdateDocument(parseResult, semantic);

        var result = new DocumentAnalysisResult(parseResult, semantic, sw.Elapsed);
        _latest[snapshot.DocumentId] = result;

        _eventBus.Publish(new DocumentAnalyzedEvent(snapshot.DocumentId, snapshot.Version, result));
        MaybePublishRequireGraph();
        return result;
    }

    private void MaybePublishRequireGraph()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRequireGraphPublishUtc).TotalMilliseconds < RequireGraphThrottleMs)
        {
            return;
        }

        _lastRequireGraphPublishUtc = now;
        _requireGraphScanner.PublishUpdated();
    }
}
