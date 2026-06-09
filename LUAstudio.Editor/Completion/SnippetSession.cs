using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.IntelliSense.Completion;

namespace LUAstudio.Editor.Completion;

public sealed class SnippetSession : IDisposable
{
    private readonly TextArea _textArea;
    private readonly SnippetExpansion _expansion;
    private int _currentIndex;
    private readonly List<SnippetPlaceholder> _ordered;

    public SnippetSession(TextArea textArea, SnippetExpansion expansion)
    {
        _textArea = textArea;
        _expansion = expansion;
        _ordered = expansion.Placeholders.OrderBy(p => p.Index).ToList();
        _currentIndex = 0;
        HighlightCurrent();
    }

    public bool TryAdvance()
    {
        if (_currentIndex >= _ordered.Count - 1)
        {
            Dispose();
            return false;
        }

        _currentIndex++;
        HighlightCurrent();
        return true;
    }

    private void HighlightCurrent()
    {
        if (_ordered.Count == 0)
        {
            return;
        }

        var ph = _ordered[_currentIndex];
        _textArea.Caret.Offset = ph.Start;
        _textArea.Selection = Selection.Create(_textArea, ph.Start, ph.Start + ph.Length);
    }

    public void Dispose()
    {
        _textArea.ClearSelection();
    }
}
