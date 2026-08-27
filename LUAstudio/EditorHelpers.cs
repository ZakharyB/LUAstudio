using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;                  
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.Editor.Debugging;
using LUAstudio.IDE.Documents;
using CustomDocument = LUAstudio.IDE.Documents.TextDocument;

namespace LUAstudio;

public class LineHighlighter : IDisposable
{
    private readonly TextView _textView;
    private DispatcherTimer? _timer;
    private TimeSpan? _highlightDuration = TimeSpan.FromSeconds(2);
    private readonly HashSet<int> _highlightedLines = new HashSet<int>();
    private readonly LineHighlightTransformer _transformer;

    public LineHighlighter(TextView textView)
    {
        _textView = textView;
        _transformer = new LineHighlightTransformer(this);
        _textView.LineTransformers.Add(_transformer);
    }

    public void SetHighlightDuration(TimeSpan? duration)
    {
        _highlightDuration = duration;
        if (_timer != null)
            _timer.Interval = duration ?? TimeSpan.Zero;
    }

    public void HighlightLine(int line)
    {
        if (line <= 0) return;
        _highlightedLines.Add(line);
        _textView.InvalidateVisual();

        if (_highlightDuration.HasValue && _highlightDuration.Value > TimeSpan.Zero)
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = _highlightDuration.Value };
                _timer.Tick += OnTimerTick;
            }
            _timer.Stop();
            _timer.Start();
        }
    }

    public void ClearHighlights()
    {
        if (_highlightedLines.Count == 0) return;
        _highlightedLines.Clear();
        _textView.InvalidateVisual();
        _timer?.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        ClearHighlights();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _textView.LineTransformers.Remove(_transformer);
    }

    private class LineHighlightTransformer : IVisualLineTransformer
    {
        private readonly LineHighlighter _owner;
        public LineHighlightTransformer(LineHighlighter owner) => _owner = owner;

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
            if (_owner._highlightedLines.Count == 0) return;
            int line = context.VisualLine.FirstDocumentLine.LineNumber;
            if (!_owner._highlightedLines.Contains(line)) return;

            foreach (var element in elements)
            {
                element.TextRunProperties.SetBackgroundBrush(Brushes.LightYellow);
            }
        }
    }
}

public class BreakpointMargin : AbstractMargin, IDisposable
{
    private readonly IBreakpointService _breakpointService;
    private string? _sourcePath;
    private double _marginWidth = 20;

    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            if (_sourcePath != value)
            {
                UnsubscribeBreakpointEvents();
                _sourcePath = value;
                SubscribeBreakpointEvents();
                InvalidateCache();
                InvalidateVisual();
            }
        }
    }

    public double MarginWidth
    {
        get => _marginWidth;
        set
        {
            if (Math.Abs(_marginWidth - value) > 0.01)
            {
                _marginWidth = value;
                Width = value;
                InvalidateVisual();
            }
        }
    }

    public BreakpointMargin(IBreakpointService service)
    {
        _breakpointService = service;
        Width = _marginWidth;
        SubscribeBreakpointEvents();
    }

    public void SetDocument(CustomDocument? doc) { }

    private void SubscribeBreakpointEvents()
    {
        if (_breakpointService != null)
            _breakpointService.BreakpointsChanged += OnBreakpointsChanged;
    }

    private void UnsubscribeBreakpointEvents()
    {
        if (_breakpointService != null)
            _breakpointService.BreakpointsChanged -= OnBreakpointsChanged;
    }

    private void OnBreakpointsChanged()
    {
        InvalidateCache();
        Dispatcher.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_sourcePath == null || TextView?.Document == null) return;

        // Derive positions from the current visual lines on every render. This
        // keeps markers attached to their document line while the view scrolls.
        var breakpoints = _breakpointService.GetBreakpointsForFile(_sourcePath)
            .Select(bp => bp.Line)
            .ToHashSet();
        foreach (var visualLine in TextView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine.LineNumber;
            if (!breakpoints.Contains(line))
                continue;

            var top = visualLine.VisualTop - TextView.VerticalOffset;
            var center = new Point(Width / 2, top + visualLine.Height / 2);
            drawingContext.DrawEllipse(Brushes.Red, new Pen(Brushes.DarkRed, 1), center, 6, 6);
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        if (newTextView != null)
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        InvalidateCache();
        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateCache();
        InvalidateVisual();
    }

    private void InvalidateCache()
    {
        // Marker geometry is calculated from visible lines during OnRender.
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_sourcePath == null || TextView == null) return;

        var pos = e.GetPosition(this);
        var line = GetLineFromY(pos.Y);
        if (line.HasValue)
            _breakpointService.ToggleBreakpoint(_sourcePath, line.Value);
    }

    private int? GetLineFromY(double y)
    {
        if (TextView == null) return null;
        foreach (var vl in TextView.VisualLines)
        {
            var top = vl.VisualTop - TextView.VerticalOffset;
            var bottom = top + vl.Height;
            if (y >= top && y <= bottom)
                return vl.FirstDocumentLine.LineNumber;
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(_marginWidth, availableSize.Height);
    }

    public void Dispose()
    {
        UnsubscribeBreakpointEvents();
    }

}

public class RelativeLineNumberMargin : AbstractMargin
{
    private readonly TextEditor _editor;

    public RelativeLineNumberMargin(TextEditor editor)
    {
        _editor = editor;
        Width = 40;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_editor.Document == null) return;

        var caretLine = _editor.TextArea.Caret.Line;
        var typeface = new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch);
        var foreground = _editor.LineNumbersForeground ?? Brushes.Gray;
        var fontSize = _editor.FontSize;
        var format = new FormattedText("0", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineNum = visualLine.FirstDocumentLine.LineNumber;
            int relative = lineNum - caretLine;
            string text = relative == 0 ? ">" : relative.ToString();
            double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextTop);
            drawingContext.DrawText(new FormattedText(text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip),
                new Point(Width - 5 - 10, y));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(40, availableSize.Height);
    }
}
