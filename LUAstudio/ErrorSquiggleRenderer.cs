using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace LUAstudio;

public class ErrorSquiggleRenderer : IBackgroundRenderer
{
    private readonly ICSharpCode.AvalonEdit.Rendering.TextView _textView;
    private readonly Pen _pen;
    private readonly List<(int Offset, int Length)> _errorSpans = new();

    public KnownLayer Layer => KnownLayer.Selection;

    public ErrorSquiggleRenderer(ICSharpCode.AvalonEdit.Rendering.TextView textView)
    {
        _textView = textView ?? throw new ArgumentNullException(nameof(textView));
        _pen = new Pen(Brushes.Red, 2.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        _pen.Freeze();
    }

    public void SetErrors(IEnumerable<(int Offset, int Length)> errors)
    {
        _errorSpans.Clear();
        if (errors != null)
            _errorSpans.AddRange(errors);
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(ICSharpCode.AvalonEdit.Rendering.TextView textView, DrawingContext drawingContext)
    {
        if (_errorSpans.Count == 0 || textView.Document == null)
            return;

        var doc = textView.Document;
        foreach (var (offset, length) in _errorSpans)
        {
            if (offset < 0 || offset + length > doc.TextLength)
                continue;

            var startLoc = doc.GetLocation(offset);
            var endLoc = doc.GetLocation(offset + length);

            if (startLoc.Line == endLoc.Line)
            {
                var line = doc.GetLineByNumber(startLoc.Line);
                var x1 = textView.GetVisualPosition(
                    new ICSharpCode.AvalonEdit.TextViewPosition(line.LineNumber, startLoc.Column),
                    ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).X;
                var x2 = textView.GetVisualPosition(
                    new ICSharpCode.AvalonEdit.TextViewPosition(line.LineNumber, endLoc.Column),
                    ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).X;
                var y = textView.GetVisualPosition(
                    new ICSharpCode.AvalonEdit.TextViewPosition(line.LineNumber, 1),
                    ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).Y - 2;

                DrawWavyLine(drawingContext, new Point(x1, y), new Point(x2, y));
            }
            else
            {
                for (int line = startLoc.Line; line <= endLoc.Line; line++)
                {
                    var docLine = doc.GetLineByNumber(line);
                    int colStart = (line == startLoc.Line) ? startLoc.Column : 1;
                    int colEnd = (line == endLoc.Line) ? endLoc.Column : docLine.Length + 1;

                    var x1 = textView.GetVisualPosition(
                        new ICSharpCode.AvalonEdit.TextViewPosition(line, colStart),
                        ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).X;
                    var x2 = textView.GetVisualPosition(
                        new ICSharpCode.AvalonEdit.TextViewPosition(line, colEnd),
                        ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).X;
                    var y = textView.GetVisualPosition(
                        new ICSharpCode.AvalonEdit.TextViewPosition(line, 1),
                        ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom).Y - 2;

                    DrawWavyLine(drawingContext, new Point(x1, y), new Point(x2, y));
                }
            }
        }
    }

    private void DrawWavyLine(DrawingContext dc, Point start, Point end)
    {
        const double amplitude = 2.5;
        const double wavelength = 5.0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            double x = start.X;
            bool up = true;
            while (x < end.X)
            {
                double nextX = Math.Min(x + wavelength / 2, end.X);
                double midX = (x + nextX) / 2;
                double yOffset = up ? amplitude : -amplitude;
                ctx.QuadraticBezierTo(new Point(midX, start.Y + yOffset),
                                      new Point(nextX, start.Y), true, false);
                x = nextX;
                up = !up;
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, _pen, geometry);
    }
}