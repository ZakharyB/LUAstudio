using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
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
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Core.Events;
using LUAstudio.Abstractions;
using LUAstudio.Core;
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
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _typeResolver;
    private readonly HoverInfoService _hover;
    private readonly SignatureHelpService _signatureHelp;
    private LuaSyntaxHighlighting? _syntaxHighlight;
    private SemanticHighlightingClassifier? _semanticHighlight;
    private TextEditor? _editor;
    private Guid _documentId;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource? _completionCts;
    private SnippetSession? _snippetSession;
    private int _popupSelectionIndex;
    private string? _filePath;
    private LuaDialect _dialect;
    private ToolTip? _hoverTip;
    private Popup? _signaturePopup;

    public EditorIntelliSenseController(
        ICompletionService completion,
        IAnalysisOrchestrator analysis,
        IDocumentSnapshotStore snapshots,
        IMainThread mainThread,
        IEventBus eventBus,
        InlineCompletionService inline,
        SmartEnterHandler smartEnter,
        AutoPairInsertService autoPairs,
        IRobloxApiDatabase roblox,
        ExpressionTypeResolver typeResolver,
        HoverInfoService hover,
        SignatureHelpService signatureHelp)
    {
        _completion = completion;
        _analysis = analysis;
        _snapshots = snapshots;
        _mainThread = mainThread;
        _eventBus = eventBus;
        _inline = inline;
        _smartEnter = smartEnter;
        _autoPairs = autoPairs;
        _roblox = roblox;
        _typeResolver = typeResolver;
        _hover = hover;
        _signatureHelp = signatureHelp;

        _eventBus.Subscribe<DocumentAnalyzedEvent>(OnDocumentAnalyzed);
    }

    public void Attach(TextEditor editor, Guid documentId, string? filePath, LuaDialect dialect)
    {
        Detach();
        _editor = editor;
        _documentId = documentId;
        _filePath = filePath;
        _dialect = dialect;

        editor.SyntaxHighlighting = null;

        _syntaxHighlight = new LuaSyntaxHighlighting(_roblox);
        editor.TextArea.TextView.LineTransformers.Add(_syntaxHighlight);

        _semanticHighlight = new SemanticHighlightingClassifier(_analysis, _roblox, _typeResolver);
        _semanticHighlight.SetDocument(documentId);
        editor.TextArea.TextView.LineTransformers.Add(_semanticHighlight);
        _inline.Attach(editor, documentId);
        _smartEnter.Attach(editor, _analysis, documentId);
        _autoPairs.Attach(editor);

        editor.TextArea.TextEntering += OnTextEntering;
        editor.TextArea.TextEntered += OnTextEntered;
        editor.TextArea.KeyDown += OnKeyDown;
        editor.TextChanged += OnEditorTextChanged;
        editor.TextArea.TextView.MouseHover += OnMouseHover;
        editor.TextArea.TextView.MouseHoverStopped += OnMouseHoverStopped;

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
        _editor.TextArea.TextView.MouseHover -= OnMouseHover;
        _editor.TextArea.TextView.MouseHoverStopped -= OnMouseHoverStopped;

        if (_semanticHighlight is not null)
        {
            _editor.TextArea.TextView.LineTransformers.Remove(_semanticHighlight);
            _semanticHighlight = null;
        }

        if (_syntaxHighlight is not null)
        {
            _editor.TextArea.TextView.LineTransformers.Remove(_syntaxHighlight);
            _syntaxHighlight = null;
        }

        _inline.Detach();
        _smartEnter.Detach();
        _autoPairs.Detach();
        CloseIntelligencePopups();
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
        if (_editor is null)
        {
            return;
        }

        // Keep completion's snapshot current synchronously.  The document view's
        // binding notification can arrive later than AvalonEdit's TextChanged.
        var snapshot = _snapshots.UpdateContent(
            _documentId, _editor.Document.Text, _filePath, _dialect);
        _analysis.RequestAnalysis(snapshot);
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

        if (e.Text is "(" or ",")
            await ShowSignatureHelpAsync().ConfigureAwait(false);
        else if (e.Text == ")")
            _mainThread.Send(CloseSignatureHelp);

        if (IsEnabled(SettingKeys.EditorAutoComplete) && ShouldTriggerCompletion(e.Text))
        {
            await ShowCompletionPopupAsync(selectIndex: 0).ConfigureAwait(false);
        }
        else if (IsEnabled(SettingKeys.EditorInlineCompletions) && IsIdentifierChar(e.Text))
        {
            _inline.OnTextChanged();
        }
    }

    private async void OnMouseHover(object? sender, MouseEventArgs e)
    {
        if (_editor is null) return;
        var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
        if (position is null) return;
        var offset = _editor.Document.GetOffset(position.Value.Location);
        var context = CreateContext(offset);
        if (context is null) return;
        var info = await _hover.GetHoverAsync(context).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(info)) return;
        _mainThread.Send(() =>
        {
            if (_editor is null) return;
            _hoverTip = new ToolTip { Content = new TextBlock { Text = info, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 } };
            _editor.ToolTip = _hoverTip;
            _hoverTip.IsOpen = true;
        });
    }

    private void OnMouseHoverStopped(object? sender, MouseEventArgs e)
    {
        if (_hoverTip is not null) _hoverTip.IsOpen = false;
        if (_editor is not null) _editor.ToolTip = null;
        _hoverTip = null;
    }

    private async Task ShowSignatureHelpAsync()
    {
        if (_editor is null) return;
        var context = CreateContext(_editor.CaretOffset);
        if (context is null) return;
        var signature = await _signatureHelp.GetSignatureAsync(context).ConfigureAwait(false);
        if (signature is null) return;
        _mainThread.Send(() =>
        {
            if (_editor is null) return;
            var text = signature.Documentation is null ? signature.Label : $"{signature.Label}\n{signature.Documentation}";
            _signaturePopup ??= new Popup { Placement = PlacementMode.Relative, PlacementTarget = _editor.TextArea, AllowsTransparency = true };
            _signaturePopup.Child = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x23)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x44)),
                BorderThickness = new Thickness(1), Padding = new Thickness(8),
                Child = new TextBlock { Text = text, Foreground = Brushes.White, MaxWidth = 520, TextWrapping = TextWrapping.Wrap }
            };
            var caret = _editor.TextArea.Caret.CalculateCaretRectangle();
            _signaturePopup.HorizontalOffset = caret.Left;
            _signaturePopup.VerticalOffset = caret.Bottom + 4;
            _signaturePopup.IsOpen = true;
        });
    }

    private CompletionContext? CreateContext(int offset)
    {
        var snapshot = _snapshots.GetSnapshot(_documentId);
        return snapshot is null ? null : new CompletionContext(snapshot, offset, _analysis.GetLatestResult(_documentId)?.SemanticModel, string.Empty);
    }

    private void CloseSignatureHelp()
    {
        if (_signaturePopup is not null) _signaturePopup.IsOpen = false;
    }

    private void CloseIntelligencePopups()
    {
        OnMouseHoverStopped(this, null!);
        CloseSignatureHelp();
        _signaturePopup = null;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (e.Key == Key.Enter && IsEnabled(SettingKeys.EditorSmartEnter) && _smartEnter.TryHandleEnter())
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

        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control &&
            IsEnabled(SettingKeys.EditorAutoComplete))
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

    private static bool IsEnabled(string key) =>
        Engine.Globals.Get<bool>(key)?.Value != false;

    private async Task ShowCompletionPopupAsync(int selectIndex)
    {
        if (_editor is null || !IsEnabled(SettingKeys.EditorAutoComplete))
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
