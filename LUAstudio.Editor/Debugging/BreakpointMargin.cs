using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace LUAstudio.Editor.Debugging;

public sealed class BreakpointRenderer : IBackgroundRenderer
{
    private readonly IBreakpointService _breakpoints;

    public BreakpointRenderer(IBreakpointService breakpoints) => _breakpoints = breakpoints;

    public KnownLayer Layer => KnownLayer.Background;

    public string? SourcePath { get; set; }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView.Document is null)
        {
            return;
        }

        foreach (var line in textView.VisualLines)
        {
            var lineNumber = line.FirstDocumentLine.LineNumber;
            if (!_breakpoints.IsBreakpoint(SourcePath, lineNumber))
            {
                continue;
            }

            var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop);
            var height = line.Height;
            drawingContext.DrawEllipse(
                Brushes.IndianRed,
                null,
                new Point(textView.ScrollOffset.X + 8, y + height / 2),
                4,
                4);
        }
    }
}

public sealed class BreakpointClickHandler
{
    private readonly IBreakpointService _breakpoints;
    private readonly BreakpointRenderer _renderer;

    public BreakpointClickHandler(IBreakpointService breakpoints, BreakpointRenderer renderer)
    {
        _breakpoints = breakpoints;
        _renderer = renderer;
    }

    public void Attach(TextEditor editor)
    {
        editor.TextArea.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 1)
            {
                return;
            }

            var position = e.GetPosition(editor.TextArea.TextView);
            if (position.X > 32)
            {
                return;
            }

            var textView = editor.TextArea.TextView;
            var visualPosition = textView.GetPosition(position + textView.ScrollOffset);
            if (visualPosition is null)
            {
                return;
            }

            var line = visualPosition.Value.Line + 1;
            _breakpoints.ToggleBreakpoint(_renderer.SourcePath, line);
            textView.InvalidateLayer(_renderer.Layer);
            e.Handled = true;
        };

        _breakpoints.BreakpointsChanged += () => editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    public void SetSourcePath(string? sourcePath)
    {
        _renderer.SourcePath = sourcePath;
    }
}
