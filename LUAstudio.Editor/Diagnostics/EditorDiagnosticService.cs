using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Events;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Parsing;

namespace LUAstudio.Editor.Diagnostics;

public sealed class EditorDiagnosticService
{
    private readonly Dictionary<Guid, List<DiagnosticMarker>> _markers = new();
    private readonly HashSet<TextEditor> _renderersRegistered = new();

    private sealed record DiagnosticMarker(
        int StartOffset,
        int EndOffset,
        DiagnosticSeverity Severity,
        string Code,
        string Message,
        string? FixSuggestion);

    public void Attach(TextEditor editor, Guid documentId)
    {
        editor.Tag = documentId;
    }
    
    
    
    

    public void ApplyDiagnostics(TextEditor editor, DocumentAnalysisResult result)
    {
        if (editor.Tag is not Guid docId || docId != result.ParseResult.Snapshot.DocumentId)
        {
            return;
        }

        ClearMarkers(editor, docId);
        var segments = new List<DiagnosticMarker>();

        foreach (var d in result.ParseResult.Tree.Diagnostics)
        {
            segments.Add(CreateMarker(editor.Document, d.Span.Start, d.Span.Length, d.Severity, d.Code, d.Message, null));
        }

        foreach (var d in result.SemanticModel.Diagnostics)
        {
            var severity = d.Severity switch
            {
                SemanticDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                SemanticDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                SemanticDiagnosticSeverity.Info => DiagnosticSeverity.Info,
                SemanticDiagnosticSeverity.Hint => DiagnosticSeverity.Hint,
                _ => DiagnosticSeverity.Warning
            };
            segments.Add(CreateMarker(editor.Document, d.Span.Start, d.Span.Length, severity, d.Code, d.Message, d.FixSuggestion));
        }

        _markers[docId] = segments;
        editor.TextArea.TextView.Redraw();
    }

    public SemanticDiagnostic? GetDiagnosticAt(Guid documentId, int offset)
    {
        if (!_markers.TryGetValue(documentId, out var segments))
        {
            return null;
        }

        foreach (var seg in segments)
        {
            var containsOffset = seg.EndOffset > seg.StartOffset
                ? offset >= seg.StartOffset && offset < seg.EndOffset
                : offset == seg.StartOffset;
            if (containsOffset)
            {
                return new SemanticDiagnostic(seg.Code, seg.Message, default, MapSeverity(seg.Severity), seg.FixSuggestion);
            }
        }

        return null;
    }

    public void ClearMarkers(TextEditor editor, Guid documentId)
    {
        _markers.Remove(documentId);
        editor.TextArea.TextView.Redraw();
    }

    private static DiagnosticMarker CreateMarker(TextDocument document, int start, int length, DiagnosticSeverity severity, string code, string message, string? fix)
    {
        var s = Math.Clamp(start, 0, document.TextLength);
        var len = Math.Clamp(length, 0, document.TextLength - s);
        if (len == 0 && s < document.TextLength)
        {
            len = 1;
        }

        return new DiagnosticMarker(s, s + len, severity, code, message, fix);
    }

    private static SemanticDiagnosticSeverity MapSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => SemanticDiagnosticSeverity.Error,
        DiagnosticSeverity.Warning => SemanticDiagnosticSeverity.Warning,
        DiagnosticSeverity.Info => SemanticDiagnosticSeverity.Info,
        DiagnosticSeverity.Hint => SemanticDiagnosticSeverity.Hint,
        _ => SemanticDiagnosticSeverity.Warning
    };

    public void RegisterMarkerRenderer(TextEditor editor)
    {
        if (!_renderersRegistered.Add(editor))
        {
            return;
        }

        editor.TextArea.TextView.LineTransformers.Add(new DiagnosticSquiggleTransformer(this, editor));
    }

    private sealed class DiagnosticSquiggleTransformer : DocumentColorizingTransformer
    {
        private readonly EditorDiagnosticService _service;
        private readonly TextEditor _editor;

        private static readonly Dictionary<DiagnosticSeverity, (Color Color, TextDecorationCollection Decoration)> Styles = CreateStyles();

        public DiagnosticSquiggleTransformer(EditorDiagnosticService service, TextEditor editor)
        {
            _service = service;
            _editor = editor;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_editor.Tag is not Guid docId || !_service._markers.TryGetValue(docId, out var segments))
            {
                return;
            }

            foreach (var seg in segments)
            {
                if (seg.EndOffset < line.Offset || seg.StartOffset > line.EndOffset)
                {
                    continue;
                }

                var start = Math.Max(seg.StartOffset, line.Offset);
                var end = Math.Min(seg.EndOffset, line.EndOffset);
                var style = Styles.GetValueOrDefault(seg.Severity, Styles[DiagnosticSeverity.Error]);

                ChangeLinePart(start, end, el =>
                {
                    el.TextRunProperties.SetTextDecorations(style.Decoration);
                    el.TextRunProperties.SetForegroundBrush(new SolidColorBrush(style.Color));
                });
            }
        }

        private static Dictionary<DiagnosticSeverity, (Color, TextDecorationCollection)> CreateStyles()
        {
            TextDecorationCollection Squiggle(Color color) =>
            [
                new TextDecoration(
                    TextDecorationLocation.Underline,
                    new Pen(new SolidColorBrush(color), 1) { DashStyle = DashStyles.Dot },
                    0,
                    TextDecorationUnit.FontRecommended,
                    TextDecorationUnit.FontRecommended)
            ];

            return new Dictionary<DiagnosticSeverity, (Color, TextDecorationCollection)>
            {
                [DiagnosticSeverity.Error] = (Color.FromRgb(0xF4, 0x43, 0x36), Squiggle(Color.FromRgb(0xF4, 0x43, 0x36))),
                [DiagnosticSeverity.Warning] = (Color.FromRgb(0xFF, 0xB7, 0x00), Squiggle(Color.FromRgb(0xFF, 0xB7, 0x00))),
                [DiagnosticSeverity.Info] = (Color.FromRgb(0x42, 0xA5, 0xF5), Squiggle(Color.FromRgb(0x42, 0xA5, 0xF5))),
                [DiagnosticSeverity.Hint] = (Color.FromRgb(0x9E, 0x9E, 0x9E), Squiggle(Color.FromRgb(0x9E, 0x9E, 0x9E)))
            };
        }
    }
}

public sealed class EditorDiagnosticHoverController
{
    private readonly EditorDiagnosticService _diagnostics;
    private readonly Popup _popup = new()
    {
        AllowsTransparency = true,
        PopupAnimation = PopupAnimation.None,
        StaysOpen = true,
        Placement = PlacementMode.Mouse
    };

    private TextEditor? _editor;
    private Guid _documentId;
    private System.Windows.Threading.DispatcherTimer? _hoverTimer;
    private int _pendingOffset = -1;
    private int _lastPointerOffset = -1;
    private string? _visibleDiagnosticKey;

    public EditorDiagnosticHoverController(EditorDiagnosticService diagnostics) => _diagnostics = diagnostics;

    public void Attach(TextEditor editor, Guid documentId)
    {
        Detach();
        _editor = editor;
        _documentId = documentId;
        editor.TextArea.MouseMove += OnMouseMove;
        editor.TextArea.MouseLeave += OnMouseLeave;
    }

    public void Detach()
    {
        if (_editor is null)
        {
            return;
        }

        _editor.TextArea.MouseMove -= OnMouseMove;
        _editor.TextArea.MouseLeave -= OnMouseLeave;
        _hoverTimer?.Stop();
        _hoverTimer = null;
        _popup.IsOpen = false;
        _visibleDiagnosticKey = null;
        _lastPointerOffset = -1;
        _editor = null;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        var pos = _editor.TextArea.TextView.GetPositionFloor(e.GetPosition(_editor.TextArea.TextView) + _editor.TextArea.TextView.ScrollOffset);
        if (pos is null)
        {
            _popup.IsOpen = false;
            return;
        }

        _pendingOffset = _editor.Document.GetOffset(pos.Value.Line, pos.Value.Column);
        if (_pendingOffset == _lastPointerOffset)
        {
            return;
        }

        _lastPointerOffset = _pendingOffset;
        _hoverTimer ??= CreateHoverTimer();
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreateHoverTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_editor is null || _pendingOffset < 0)
            {
                return;
            }

            var diagnostic = _diagnostics.GetDiagnosticAt(_documentId, _pendingOffset);
            if (diagnostic is null)
            {
                _popup.IsOpen = false;
                _visibleDiagnosticKey = null;
                return;
            }

            var key = $"{diagnostic.Code}\0{diagnostic.Message}\0{diagnostic.FixSuggestion}";
            if (_popup.IsOpen && string.Equals(_visibleDiagnosticKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _visibleDiagnosticKey = key;
            ShowTooltip(diagnostic);
        };
        return timer;
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoverTimer?.Stop();
        _popup.IsOpen = false;
        _visibleDiagnosticKey = null;
        _lastPointerOffset = -1;
    }

    private void ShowTooltip(SemanticDiagnostic diagnostic)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x2F)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x48, 0x4F)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            MaxWidth = 420,
            Child = BuildContent(diagnostic)
        };

        _popup.Child = border;
        _popup.IsOpen = true;
    }

    private static StackPanel BuildContent(SemanticDiagnostic diagnostic)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"{diagnostic.Code}: {diagnostic.Message}",
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold
        });

        if (!string.IsNullOrEmpty(diagnostic.FixSuggestion))
        {
            panel.Children.Add(new TextBlock
            {
                Text = diagnostic.FixSuggestion,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xB0, 0xBD)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            panel.Children.Add(new Button
            {
                Content = "Quick Fix",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                IsEnabled = false,
                ToolTip = "Quick fixes coming soon"
            });
        }

        return panel;
    }
}
