using System.Windows;
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
    private readonly Dictionary<Guid, List<DiagnosticSegment>> _markers = new();

    private readonly record struct DiagnosticSegment(int StartOffset, int EndOffset);

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
        var segments = new List<DiagnosticSegment>();

        foreach (var d in result.ParseResult.Tree.Diagnostics)
        {
            if (d.Severity >= DiagnosticSeverity.Error)
            {
                segments.Add(CreateSegment(editor.Document, d.Span.Start, d.Span.Length));
            }
        }

        foreach (var d in result.SemanticModel.Diagnostics)
        {
            if (d.Severity >= SemanticDiagnosticSeverity.Warning)
            {
                segments.Add(CreateSegment(editor.Document, d.Span.Start, d.Span.Length));
            }
        }

        _markers[docId] = segments;
        editor.TextArea.TextView.Redraw();
    }

    public void ClearMarkers(TextEditor editor, Guid documentId)
    {
        _markers.Remove(documentId);
        editor.TextArea.TextView.Redraw();
    }

    private static DiagnosticSegment CreateSegment(TextDocument document, int start, int length)
    {
        var s = Math.Clamp(start, 0, document.TextLength);
        var len = Math.Clamp(length, 0, document.TextLength - s);
        return new DiagnosticSegment(s, s + len);
    }

    public void RegisterMarkerRenderer(TextEditor editor)
    {
        editor.TextArea.TextView.LineTransformers.Add(new DiagnosticLineTransformer(this, editor));
    }

    private sealed class DiagnosticLineTransformer : DocumentColorizingTransformer
    {
        private readonly EditorDiagnosticService _service;
        private readonly TextEditor _editor;

        public DiagnosticLineTransformer(EditorDiagnosticService service, TextEditor editor)
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

            var errorBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF4, 0x43, 0x36));
            errorBrush.Freeze();

            foreach (var seg in segments)
            {
                if (seg.EndOffset < line.Offset || seg.StartOffset > line.EndOffset)
                {
                    continue;
                }

                var start = Math.Max(seg.StartOffset, line.Offset);
                var end = Math.Min(seg.EndOffset, line.EndOffset);
                ChangeLinePart(start, end, el =>
                {
                    el.TextRunProperties.SetBackgroundBrush(errorBrush);
                });
            }
        }
    }
}

public sealed class DocumentAnalyzedEditorHandler
{
    private readonly EditorDiagnosticService _diagnostics;

    public DocumentAnalyzedEditorHandler(
        LUAstudio.Core.Events.IEventBus eventBus,
        EditorDiagnosticService diagnostics)
    {
        _diagnostics = diagnostics;
        eventBus.Subscribe<DocumentAnalyzedEvent>(_ => { });
    }
}
