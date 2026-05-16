using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.Core.Threading;
using LUAstudio.Editor.Completion;
using LUAstudio.Editor.Editing;
using LUAstudio.Editor.Highlighting;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Completion;
using LUAstudio.IntelliSense.Documents;
using LUAstudio.IntelliSense.Events;
using LUAstudio.Core.Events;
using LUAstudio.Languages.Text;

namespace LUAstudio.Editor.IntelliSense;

public sealed class EditorIntelliSenseController : IDisposable
{
    private readonly ICompletionService _completion;
    private readonly IAnalysisOrchestrator _analysis;
    private readonly IDocumentSnapshotStore _snapshots;
    private readonly IMainThread _mainThread;
    private readonly IEventBus _eventBus;
    private readonly InlineCompletionService _inline;
    private readonly SmartEnterHandler _smartEnter;
    private readonly AutoPairInsertService _autoPairs;
    private readonly SyntaxHighlightingService _syntax;
    private readonly SemanticHighlightingClassifier _semanticHighlight;
    private TextEditor? _editor;
    private Guid _documentId;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource? _completionCts;
    private SnippetSession? _snippetSession;
    private int _popupSelectionIndex;

    public EditorIntelliSenseController(
        ICompletionService completion,
        IAnalysisOrchestrator analysis,
        IDocumentSnapshotStore snapshots,
        IMainThread mainThread,
        IEventBus eventBus,
        InlineCompletionService inline,
        SmartEnterHandler smartEnter,
        AutoPairInsertService autoPairs,
        SyntaxHighlightingService syntax,
        SemanticHighlightingClassifier semanticHighlight)
    {
        _completion = completion;
        _analysis = analysis;
        _snapshots = snapshots;
        _mainThread = mainThread;
        _eventBus = eventBus;
        _inline = inline;
        _smartEnter = smartEnter;
        _autoPairs = autoPairs;
        _syntax = syntax;
        _semanticHighlight = semanticHighlight;

        _eventBus.Subscribe<DocumentAnalyzedEvent>(OnDocumentAnalyzed);
    }

    public void Attach(TextEditor editor, Guid documentId, string? filePath, LuaDialect dialect)
    {
        Detach();
        _editor = editor;
        _documentId = documentId;

        _syntax.Apply(editor);
        _semanticHighlight.SetDocument(documentId);
        editor.TextArea.TextView.LineTransformers.Add(_semanticHighlight);
        _inline.Attach(editor, documentId);
        _smartEnter.Attach(editor);
        _autoPairs.Attach(editor);

        editor.TextArea.TextEntering += OnTextEntering;
        editor.TextArea.TextEntered += OnTextEntered;
        editor.TextArea.KeyDown += OnKeyDown;
        editor.TextChanged += OnEditorTextChanged;

        var snapshot = _snapshots.UpdateContent(documentId, editor.Document.Text, filePath, dialect);
        _analysis.RequestAnalysis(snapshot);
    }

    public void DetachIfEditor(TextEditor editor)
    {
        if (_editor == editor)
        {
            Detach();
        }
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
        _editor.TextChanged -= OnEditorTextChanged;
        _inline.Detach();
        _smartEnter.Detach();
        _autoPairs.Detach();
        CloseCompletion();
        _snippetSession?.Dispose();
        _snippetSession = null;
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

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _inline.OnTextChanged();

        if (_completionWindow is not null)
        {
            _ = RefreshCompletionPopupAsync(selectIndex: _popupSelectionIndex);
        }
    }

    private void OnDocumentAnalyzed(DocumentAnalyzedEvent e)
    {
        if (e.DocumentId != _documentId || _editor is null)
        {
            return;
        }

        _mainThread.Send(() => _editor.TextArea.TextView.Redraw());
    }

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow is not null &&
            !char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
        {
            _completionWindow.CompletionList.RequestInsertion(e);
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
            await ShowCompletionPopupAsync(selectIndex: 0).ConfigureAwait(false);
        }
        else if (IsIdentifierChar(e.Text))
        {
            _inline.OnTextChanged();
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (e.Key == Key.Enter && _smartEnter.TryHandleEnter())
        {
            e.Handled = true;
            return;
        }

        if (_completionWindow is not null && e.Key is Key.Up or Key.Down)
        {
            var count = _completionWindow.CompletionList.CompletionData.Count;
            if (count > 0)
            {
                _popupSelectionIndex = e.Key == Key.Down
                    ? Math.Min(_popupSelectionIndex + 1, count - 1)
                    : Math.Max(_popupSelectionIndex - 1, 0);

                _completionWindow.CompletionList.ListBox.SelectedIndex = _popupSelectionIndex;
                SyncGhostToPopupSelection();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Down && _completionWindow is null && _inline.HasActiveSuggestion)
        {
            e.Handled = true;
            await ShowCompletionPopupAsync(selectIndex: 0).ConfigureAwait(false);
            return;
        }

        if (e.Key == Key.Tab)
        {
            if (_completionWindow is not null)
            {
                AcceptSelectedCompletion();
                e.Handled = true;
                return;
            }

            if (_inline.TryAcceptTab(out var session))
            {
                _snippetSession = session;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            if (_completionWindow is not null)
            {
                CloseCompletion();
                e.Handled = true;
            }
            else if (_inline.HasActiveSuggestion)
            {
                _inline.Dismiss();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await ShowCompletionPopupAsync(selectIndex: 0).ConfigureAwait(false);
        }
    }

    private void AcceptSelectedCompletion()
    {
        if (_editor is null)
        {
            return;
        }

        if (_inline.TryAcceptTab(out var session))
        {
            _snippetSession = session;
            CloseCompletion();
            return;
        }

        if (_completionWindow?.CompletionList.SelectedItem is LuaCompletionData data)
        {
            var caret = _editor.CaretOffset;
            var start = InlineCompletionService.GetIdentifierStart(_editor.Document.Text, caret);
            var length = caret - start;
            _editor.Document.Replace(start, length, data.Text);
            CloseCompletion();
            _inline.Dismiss();
        }
    }

    private void SyncGhostToPopupSelection()
    {
        if (_completionWindow?.CompletionList.SelectedItem is LuaCompletionData data)
        {
            _inline.PreviewItem(data.Item);
        }
    }

    private static bool ShouldTriggerCompletion(string text) =>
        text is "." or ":" or "(" or "\"" or "'";

    private static bool IsIdentifierChar(string text) =>
        text.Length == 1 && (char.IsLetterOrDigit(text[0]) || text[0] == '_');

    private async Task ShowCompletionPopupAsync(int selectIndex)
    {
        if (_editor is null)
        {
            return;
        }

        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        var offset = _editor.CaretOffset;
        var prefix = InlineCompletionService.GetIdentifierPrefix(_editor.Document.Text, offset);
        var triggerKind = DetectTriggerKind(_editor.Document.Text, offset);
        var snapshot = _snapshots.GetSnapshot(_documentId);
        if (snapshot is null)
        {
            return;
        }

        var analysis = _analysis.GetLatestResult(_documentId);
        var context = new CompletionContext(snapshot, offset, analysis?.SemanticModel, prefix, triggerKind);

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

        var limited = items.Take(100).ToArray();
        _popupSelectionIndex = Math.Clamp(selectIndex, 0, limited.Length - 1);

        _mainThread.Send(() =>
        {
            if (_editor is null || token.IsCancellationRequested)
            {
                return;
            }

            if (_completionWindow is null)
            {
                _completionWindow = CreateStyledCompletionWindow(_editor.TextArea);
                _completionWindow.Closed += (_, _) => _completionWindow = null;
            }
            else
            {
                _completionWindow.CompletionList.CompletionData.Clear();
            }

            foreach (var item in limited.Select(i => new LuaCompletionData(i)))
            {
                _completionWindow.CompletionList.CompletionData.Add(item);
            }

            _completionWindow.CompletionList.ListBox.SelectedIndex = _popupSelectionIndex;
            _completionWindow.Show();
            SyncGhostToPopupSelection();
        });
    }

    private async Task RefreshCompletionPopupAsync(int selectIndex)
    {
        if (_completionWindow is null)
        {
            return;
        }

        await ShowCompletionPopupAsync(selectIndex).ConfigureAwait(false);
    }

    private static CompletionTriggerKind DetectTriggerKind(string text, int offset)
    {
        if (offset <= 0)
        {
            return CompletionTriggerKind.Default;
        }

        return text[offset - 1] switch
        {
            '.' => CompletionTriggerKind.Dot,
            ':' => CompletionTriggerKind.Colon,
            '(' => CompletionTriggerKind.Invoke,
            '"' or '\'' => CompletionTriggerKind.String,
            _ => CompletionTriggerKind.Default
        };
    }

    private static CompletionWindow CreateStyledCompletionWindow(TextArea textArea)
    {
        var window = new CompletionWindow(textArea);
        window.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x23));
        window.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x44));
        window.BorderThickness = new Thickness(1);
        window.MaxHeight = 280;
        window.Padding = new Thickness(4);
        return window;
    }

    private void CloseCompletion()
    {
        _completionCts?.Cancel();
        _completionWindow?.Close();
        _completionWindow = null;
    }

    public void Dispose()
    {
        Detach();
        _inline.Dispose();
    }
}
