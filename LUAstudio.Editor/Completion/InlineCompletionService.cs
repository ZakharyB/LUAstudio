using ICSharpCode.AvalonEdit;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Completion;
using LUAstudio.IntelliSense.Documents;

namespace LUAstudio.Editor.Completion;

public sealed class InlineCompletionService : IDisposable
{
    private readonly ICompletionService _completion;
    private readonly IAnalysisOrchestrator _analysis;
    private readonly IDocumentSnapshotStore _snapshots;
    private readonly GhostTextRenderer _renderer = new();
    private TextEditor? _editor;
    private Guid _documentId;
    private CancellationTokenSource? _debounceCts;
    private CompletionItem? _currentItem;
    private IReadOnlyList<CompletionItem> _lastItems = Array.Empty<CompletionItem>();

    public GhostTextRenderer Renderer => _renderer;

    public bool HasActiveSuggestion => _currentItem is not null;

    public InlineCompletionService(
        ICompletionService completion,
        IAnalysisOrchestrator analysis,
        IDocumentSnapshotStore snapshots)
    {
        _completion = completion;
        _analysis = analysis;
        _snapshots = snapshots;
    }

    public void Attach(TextEditor editor, Guid documentId)
    {
        Detach();
        _editor = editor;
        _documentId = documentId;
        editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
    }

    public void Detach()
    {
        _debounceCts?.Cancel();
        if (_editor is not null)
        {
            _editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
        }

        Clear();
        _editor = null;
    }

    public void OnTextChanged() => RequestUpdate(selectedIndex: 0);

    public void SetSelectedIndex(int index) => RequestUpdate(index);

    public bool TryAcceptTab(out SnippetSession? snippetSession)
    {
        snippetSession = null;
        if (_editor is null || _currentItem is null)
        {
            return false;
        }

        var caret = _editor.CaretOffset;
        var prefix = GetIdentifierPrefix(_editor.Document.Text, caret);
        var start = caret - prefix.Length;
        var length = Math.Max(0, caret - start);

        string insert;
        if (_currentItem.Kind == CompletionItemKind.Snippet && SnippetEngine.ContainsPlaceholders(_currentItem.InsertText))
        {
            var expansion = SnippetEngine.Expand(_currentItem.InsertText);
            insert = expansion.Text;
            if (expansion.Placeholders.Count > 0)
            {
                snippetSession = new SnippetSession(_editor.TextArea, expansion);
            }
        }
        else
        {
            insert = _currentItem.InsertText;
        }

        _editor.Document.Replace(start, length, insert);
        Clear();
        _editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
        return true;
    }

    public void PreviewItem(CompletionItem item)
    {
        if (_editor is null)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var prefix = GetIdentifierPrefix(_editor.Document.Text, caret);
        _currentItem = item;
        _renderer.SetGhostText(caret, prefix, item.DisplayText);
        _editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    public void Dismiss()
    {
        Clear();
        _editor?.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    private void RequestUpdate(int selectedIndex)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var cts = _debounceCts;
        _ = DebouncedUpdateAsync(selectedIndex, cts.Token);
    }

    private async Task DebouncedUpdateAsync(int selectedIndex, CancellationToken token)
    {
        try
        {
            await Task.Delay(50, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_editor is null)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var prefix = GetIdentifierPrefix(_editor.Document.Text, caret);
        if (prefix.Length < 1)
        {
            Dismiss();
            return;
        }

        var snapshot = _snapshots.GetSnapshot(_documentId);
        if (snapshot is null)
        {
            return;
        }

        var analysis = _analysis.GetLatestResult(_documentId);
        var context = new CompletionContext(snapshot, caret, analysis?.SemanticModel, prefix);

        IReadOnlyList<CompletionItem> items;
        try
        {
            items = await _completion.GetCompletionsAsync(context, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var filtered = items
            .Where(i => i.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.Priority)
            .ToArray();

        if (filtered.Length == 0)
        {
            Dismiss();
            return;
        }

        _lastItems = filtered;
        var index = Math.Clamp(selectedIndex, 0, filtered.Length - 1);
        var match = filtered[index];
        _currentItem = match;
        _renderer.SetGhostText(caret, prefix, match.DisplayText);
        _editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    private void Clear()
    {
        _currentItem = null;
        _lastItems = Array.Empty<CompletionItem>();
        _renderer.Clear();
    }

    public static string GetIdentifierPrefix(string text, int offset)
    {
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return text[start..offset];
    }

    public static int GetIdentifierStart(string text, int offset) =>
        offset - GetIdentifierPrefix(text, offset).Length;

    public void Dispose() => Detach();
}
