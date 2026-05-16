using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace LUAstudio.Editor.Completion;

public sealed class GhostTextRenderer : IBackgroundRenderer
{
    private static readonly SolidColorBrush GhostBrush = CreateGhostBrush();
    private string _ghostSuffix = string.Empty;
    private int _ghostStartOffset;

    public KnownLayer Layer => KnownLayer.Selection;

    public bool HasGhostText => _ghostSuffix.Length > 0;

    public int GhostStartOffset => _ghostStartOffset;

    public string GhostSuffix => _ghostSuffix;

    /// <summary>
    /// Draw ghost suffix after the typed prefix at caretOffset.
    /// </summary>
    public void SetGhostText(int caretOffset, string prefix, string fullText)
    {
        if (string.IsNullOrEmpty(fullText) || fullText.Length <= prefix.Length ||
            !fullText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return;
        }

        _ghostStartOffset = caretOffset;
        _ghostSuffix = fullText[prefix.Length..];
    }

    public void Clear()
    {
        _ghostSuffix = string.Empty;
        _ghostStartOffset = 0;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!HasGhostText || textView.Document is null)
        {
            return;
        }

        var endOffset = _ghostStartOffset + _ghostSuffix.Length;
        if (_ghostStartOffset < 0 || endOffset > textView.Document.TextLength)
        {
            return;
        }

        var segment = new TextSegment { StartOffset = _ghostStartOffset, Length = _ghostSuffix.Length };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            var pos = rect.BottomLeft;
            var editor = textView.GetService(typeof(TextEditor)) as TextEditor;
            var fontFamily = editor?.FontFamily ?? new FontFamily("Cascadia Code");
            var fontSize = editor?.FontSize ?? 14;

            var formatted = new FormattedText(
                _ghostSuffix,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                fontSize,
                GhostBrush,
                VisualTreeHelper.GetDpi(textView).PixelsPerDip);

            drawingContext.DrawText(formatted, pos);
        }
    }

    private static SolidColorBrush CreateGhostBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5D, 0x66));
        brush.Freeze();
        return brush;
    }
}
