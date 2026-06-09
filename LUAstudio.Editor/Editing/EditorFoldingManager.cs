using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace LUAstudio.Editor.Editing;

public sealed class EditorFoldingManager : IDisposable
{
    private static readonly Regex FunctionStartRegex = new(
        @"^\s*(local\s+)?function\b",
        RegexOptions.Compiled);

    private static readonly Regex IfThenRegex = new(
        @"\bif\b.+\bthen\b",
        RegexOptions.Compiled);

    private static readonly Regex ForDoRegex = new(
        @"\bfor\b.+\bdo\b",
        RegexOptions.Compiled);

    private static readonly Regex WhileDoRegex = new(
        @"\bwhile\b.+\bdo\b",
        RegexOptions.Compiled);

    private static readonly Regex RepeatStartRegex = new(
        @"^\s*repeat\b",
        RegexOptions.Compiled);

    private static readonly Regex StandaloneDoRegex = new(
        @"^\s*do\s*$",
        RegexOptions.Compiled);

    private static readonly Regex BlockEndRegex = new(
        @"^\s*(end\b|until\b)",
        RegexOptions.Compiled);

    private FoldingManager? _manager;
    private TextDocument? _document;
    private TextEditor? _editor;

    public void Attach(TextEditor editor)
    {
        if (_editor == editor && _manager is not null)
        {
            _document!.Changed -= OnDocumentChanged;
            _document = editor.Document;
            _document.Changed += OnDocumentChanged;
            UpdateFoldings();
            return;
        }

        Detach();
        _editor = editor;
        _manager = FoldingManager.Install(editor.TextArea);
        _document = editor.Document;
        _document.Changed += OnDocumentChanged;
        ApplyMarginChrome(editor);
        UpdateFoldings();
    }

    public void Detach()
    {
        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
        }

        if (_manager is not null)
        {
            FoldingManager.Uninstall(_manager);
        }

        _manager = null;
        _document = null;
        _editor = null;
    }

    private static void ApplyMarginChrome(TextEditor editor)
    {
        var markerBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9D, 0xA5));
        markerBrush.Freeze();
        var selectedMarkerBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xE7, 0xEA));
        selectedMarkerBrush.Freeze();

        FoldingMargin.SetFoldingMarkerBrush(editor, markerBrush);
        FoldingMargin.SetSelectedFoldingMarkerBrush(editor, selectedMarkerBrush);
        FoldingMargin.SetFoldingMarkerBackgroundBrush(editor, Brushes.Transparent);
        FoldingMargin.SetSelectedFoldingMarkerBackgroundBrush(editor, Brushes.Transparent);

        foreach (var margin in editor.TextArea.LeftMargins)
        {
            if (margin is not FoldingMargin foldingMargin)
            {
                continue;
            }

            foldingMargin.FoldingMarkerBrush = markerBrush;
            foldingMargin.SelectedFoldingMarkerBrush = selectedMarkerBrush;
            foldingMargin.FoldingMarkerBackgroundBrush = Brushes.Transparent;
            foldingMargin.SelectedFoldingMarkerBackgroundBrush = Brushes.Transparent;
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => UpdateFoldings();

    private void UpdateFoldings()
    {
        if (_manager is null || _document is null)
        {
            return;
        }

        var foldings = new List<NewFolding>();
        var stack = new Stack<int>();

        for (var i = 0; i < _document.LineCount; i++)
        {
            var line = _document.GetLineByNumber(i + 1);
            var lineText = _document.GetText(line);
            var trimmed = lineText.TrimEnd();

            if (IsBlockStart(trimmed))
            {
                stack.Push(i + 1);
            }

            if (BlockEndRegex.IsMatch(trimmed) && stack.Count > 0)
            {
                var startLine = stack.Pop();
                var endLineNumber = i + 1;
                if (endLineNumber <= startLine)
                {
                    continue;
                }

                var headerLine = _document.GetLineByNumber(startLine);
                var endLine = _document.GetLineByNumber(endLineNumber);

                // Fold only body text: keep the opening line and the closing end/until visible.
                var foldStart = headerLine.EndOffset;
                var foldEnd = endLine.Offset;
                if (foldStart >= foldEnd)
                {
                    continue;
                }

                var name = GetFoldingName(_document, startLine);
                foldings.Add(new NewFolding(foldStart, foldEnd) { Name = name });
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        _manager.UpdateFoldings(foldings, -1);
    }

    private static bool IsBlockStart(string trimmed)
    {
        if (FunctionStartRegex.IsMatch(trimmed))
        {
            return true;
        }

        if (IfThenRegex.IsMatch(trimmed))
        {
            return true;
        }

        if (ForDoRegex.IsMatch(trimmed) || WhileDoRegex.IsMatch(trimmed))
        {
            return true;
        }

        if (RepeatStartRegex.IsMatch(trimmed) || StandaloneDoRegex.IsMatch(trimmed))
        {
            return true;
        }

        return false;
    }

    private static string GetFoldingName(TextDocument document, int startLine)
    {
        var line = document.GetLineByNumber(startLine);
        var text = document.GetText(line).Trim();
        if (FunctionStartRegex.IsMatch(text))
        {
            return text;
        }

        if (IfThenRegex.IsMatch(text))
        {
            return text;
        }

        return "...";
    }

    public void Dispose() => Detach();
}
