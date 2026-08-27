using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Editor.Completion;

public sealed class GhostTextRenderer : IBackgroundRenderer, IDisposable
{
    private const int MaxGhostTextLength = 200;

    private readonly Dispatcher _dispatcher;
    private object? _globalSubscription;
    private Action<string>? _colorChangedHandler;

    private SolidColorBrush _ghostBrush;
    private string _ghostSuffix = string.Empty;
    private ITextAnchor? _ghostAnchor;

    private FormattedText? _cachedFormattedText;
    private string? _cachedSuffix;
    private FontFamily? _cachedFontFamily;
    private double _cachedFontSize;
    private Brush? _cachedBrush;
    private double _cachedDpiScale;

    private TextEditor? _cachedEditor;
    private TextView? _lastTextView;

    public KnownLayer Layer => KnownLayer.Text;

    public bool HasGhostText => _ghostSuffix.Length > 0;

    public GhostTextRenderer()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        var colorSetting = Engine.Globals.Get<string>(SettingKeys.EditorColorGhostText);
        if (colorSetting != null)
        {
            _colorChangedHandler = OnGhostColorChanged;
            colorSetting.Changed += _colorChangedHandler;
            _globalSubscription = colorSetting;
        }

        _ghostBrush = CreateGhostBrush();
    }

    public void SetGhostText(TextDocument document, int caretOffset, string prefix, string fullText)
    {
        if (string.IsNullOrEmpty(fullText) || fullText.Length <= prefix.Length ||
            !fullText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return;
        }

        _ghostSuffix = fullText.Substring(prefix.Length);
        if (_ghostSuffix.Length > MaxGhostTextLength)
            _ghostSuffix = _ghostSuffix.Substring(0, MaxGhostTextLength);

        if (_ghostAnchor != null)
            _ghostAnchor.Deleted -= OnAnchorDeleted;
        _ghostAnchor = document.CreateAnchor(caretOffset);
        _ghostAnchor.Deleted += OnAnchorDeleted;

        _cachedFormattedText = null;
        InvalidateLastView();
    }

    public void Clear()
    {
        if (_ghostAnchor != null)
        {
            _ghostAnchor.Deleted -= OnAnchorDeleted;
            _ghostAnchor = null;
        }
        _ghostSuffix = string.Empty;
        _cachedFormattedText = null;
        InvalidateLastView();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        _lastTextView = textView;

        if (!HasGhostText || _ghostAnchor == null || _ghostAnchor.IsDeleted)
            return;

        int offset = _ghostAnchor.Offset;
        if (offset < 0 || offset > textView.Document.TextLength)
            return;

        EnsureEditorCached(textView);

        var fontFamily = _cachedEditor?.FontFamily ?? SystemFonts.MessageFontFamily;
        var fontSize = _cachedEditor?.FontSize ?? 14;
        var dpiScale = VisualTreeHelper.GetDpi(textView).PixelsPerDip;

        if (_cachedFormattedText == null ||
            _cachedSuffix != _ghostSuffix ||
            _cachedFontFamily != fontFamily ||
            Math.Abs(_cachedFontSize - fontSize) > 0.001 ||
            _cachedBrush != _ghostBrush ||
            Math.Abs(_cachedDpiScale - dpiScale) > 0.001)
        {
            _cachedFormattedText = new FormattedText(
                _ghostSuffix,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                fontSize,
                _ghostBrush,
                dpiScale);

            _cachedSuffix = _ghostSuffix;
            _cachedFontFamily = fontFamily;
            _cachedFontSize = fontSize;
            _cachedBrush = _ghostBrush;
            _cachedDpiScale = dpiScale;
        }

        var location = textView.Document.GetLocation(offset);
        var pos = textView.GetVisualPosition(
            new TextViewPosition(location),
            VisualYPosition.Baseline);

        drawingContext.DrawText(_cachedFormattedText, pos);
    }

    private void EnsureEditorCached(TextView textView)
    {
        if (_cachedEditor == null)
            _cachedEditor = textView.GetService(typeof(TextEditor)) as TextEditor;
    }

    private void InvalidateLastView()
    {
        _lastTextView?.InvalidateVisual();
    }

    private void OnAnchorDeleted(object? sender, EventArgs e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            _ghostSuffix = string.Empty;
            _ghostAnchor = null;
            _cachedFormattedText = null;
            InvalidateLastView();
        });
    }

    private void OnGhostColorChanged(string newValue)
    {
        _dispatcher.InvokeAsync(() =>
        {
            _ghostBrush = CreateGhostBrush();
            _cachedFormattedText = null;
            InvalidateLastView();
        });
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

    public void Dispose()
    {
        if (_ghostAnchor != null)
        {
            _ghostAnchor.Deleted -= OnAnchorDeleted;
            _ghostAnchor = null;
        }

        if (_globalSubscription != null && _colorChangedHandler != null)
        {
            var setting = _globalSubscription;
            setting.GetType().GetEvent("Changed")?.RemoveEventHandler(setting, _colorChangedHandler);
        }
    }
}
