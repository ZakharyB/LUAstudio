using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Editor.Completion;

public sealed class GhostTextRenderer : IBackgroundRenderer
{
    private SolidColorBrush _ghostBrush = CreateGhostBrush();
    private string _ghostSuffix = string.Empty;
    private int _ghostStartOffset;

    public KnownLayer Layer => KnownLayer.Selection;

    public bool HasGhostText => _ghostSuffix.Length > 0;

    public int GhostStartOffset => _ghostStartOffset;

    public string GhostSuffix => _ghostSuffix;

    public GhostTextRenderer()
    {
        var global = Engine.Globals.Get<string>(SettingKeys.EditorColorGhostText);
        if (global is not null)
        {
            global.Changed += _ => _ghostBrush = CreateGhostBrush();
        }
    }

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
                _ghostBrush,
                VisualTreeHelper.GetDpi(textView).PixelsPerDip);

            drawingContext.DrawText(formatted, pos);
        }
    }

    private static SolidColorBrush CreateGhostBrush()
    {
        var hex = Engine.Globals.Get<string>(SettingKeys.EditorColorGhostText)?.Value;
        var rgb = SettingColorParser.ParseRgb(hex, 0x5A5D66);
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF)));
        brush.Freeze();
        return brush;
    }
}
