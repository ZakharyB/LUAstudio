using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using LUAstudio.Core.Threading;
using LUAstudio.Editor.Completion;
using LUAstudio.Editor.Highlighting;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Completion;
using LUAstudio.IntelliSense.Documents;
using LUAstudio.Languages.Text;

namespace LUAstudio.Editor.IntelliSense;

/// <summary>
/// Wires AvalonEdit to completion, highlighting, and caret-aware analysis triggers.
/// </summary>
public sealed class EditorIntelliSenseController : IDisposable
{
    private readonly ICompletionService _completion;
    private readonly IAnalysisOrchestrator _analysis;
    private readonly IDocumentSnapshotStore _snapshots;
    private readonly IMainThread _mainThread;
    private TextEditor? _editor;
    private Guid _documentId;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource? _completionCts;

    public EditorIntelliSenseController(
        ICompletionService completion,
        IAnalysisOrchestrator analysis,
        IDocumentSnapshotStore snapshots,
        IMainThread mainThread)
    {
        _completion = completion;
        _analysis = analysis;
        _snapshots = snapshots;
        _mainThread = mainThread;
    }

    public void Attach(TextEditor editor, Guid documentId, string? filePath, LuaDialect dialect)
    {
        Detach();
        _editor = editor;
        _documentId = documentId;

        editor.TextArea.TextView.LineTransformers.Add(new LuaSyntaxHighlighting());
        editor.TextArea.TextEntering += OnTextEntering;
        editor.TextArea.TextEntered += OnTextEntered;
        editor.TextArea.KeyDown += OnKeyDown;

        var snapshot = _snapshots.UpdateContent(documentId, editor.Document.Text, filePath, dialect);
        _analysis.RequestAnalysis(snapshot);
    }

    public void Detach()
    {
        if (_editor is null)
        {
            return;
        }

        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.KeyDown -= OnKeyDown;
        CloseCompletion();
        _editor = null;
    }

    public void OnTextChanged(string content, string? filePath, LuaDialect dialect)
    {
        if (_editor is null)
        {
            return;
        }

        var snapshot = _snapshots.UpdateContent(_documentId, content, filePath, dialect);
        _analysis.RequestAnalysis(snapshot);
    }

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow is not null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private async void OnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (ShouldTriggerCompletion(e.Text))
        {
            await ShowCompletionAsync().ConfigureAwait(false);
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await ShowCompletionAsync().ConfigureAwait(false);
        }
    }

    private static bool ShouldTriggerCompletion(string text) =>
        text is "." or ":" or "(" or "\"" or "'";

    private async Task ShowCompletionAsync()
    {
        if (_editor is null)
        {
            return;
        }

        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        var offset = _editor.CaretOffset;
        var prefix = GetIdentifierPrefix(_editor.Document.Text, offset);
        var snapshot = _snapshots.GetSnapshot(_documentId);
        if (snapshot is null)
        {
            return;
        }

        var analysis = _analysis.GetLatestResult(_documentId);
        var context = new CompletionContext(snapshot, offset, analysis?.SemanticModel, prefix);

        IReadOnlyList<CompletionItem> items;
        try
        {
            items = await _completion.GetCompletionsAsync(context, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (items.Count == 0)
        {
            return;
        }

        _mainThread.Send(() =>
        {
            if (_editor is null || token.IsCancellationRequested)
            {
                return;
            }

            CloseCompletion();
            _completionWindow = new CompletionWindow(_editor.TextArea);
            var data = items.Select(i => new LuaCompletionData(i)).ToArray();
            foreach (var item in data)
            {
                _completionWindow.CompletionList.CompletionData.Add(item);
            }

            _completionWindow.Show();
            _completionWindow.Closed += (_, _) => _completionWindow = null;
        });
    }

    private void CloseCompletion()
    {
        _completionCts?.Cancel();
        _completionWindow?.Close();
        _completionWindow = null;
    }

    private static string GetIdentifierPrefix(string text, int offset)
    {
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return text[start..offset];
    }

    public void Dispose() => Detach();
}
