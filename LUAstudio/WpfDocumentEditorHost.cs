using ICSharpCode.AvalonEdit;
using LUAstudio.Editor.Diagnostics;
using LUAstudio.Editor.IntelliSense;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Handlers;
using LUAstudio.IntelliSense.Events;
using LUAstudio.Core.Events;
using LUAstudio.Languages.Text;

namespace LUAstudio;

public sealed class WpfDocumentEditorHost : IDisposable
{
    private readonly EditorIntelliSenseController _intelliSense;
    private readonly EditorDiagnosticService _diagnostics;
    private readonly DocumentAnalysisHandler _analysisHandler;
    private readonly Dictionary<Guid, TextEditor> _editors = new();

    public WpfDocumentEditorHost(
        EditorIntelliSenseController intelliSense,
        EditorDiagnosticService diagnostics,
        DocumentAnalysisHandler analysisHandler,
        IEventBus eventBus)
    {
        _intelliSense = intelliSense;
        _diagnostics = diagnostics;
        _analysisHandler = analysisHandler;
        eventBus.Subscribe<DocumentAnalyzedEvent>(OnDocumentAnalyzed);
    }

    public void Attach(TextEditor editor, TextDocument document)
    {
        var dialect = document.FilePath?.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) == true
            ? LuaDialect.Luau
            : LuaDialect.Lua;

        _editors[document.Id] = editor;
        _diagnostics.Attach(editor, document.Id);
        _diagnostics.RegisterMarkerRenderer(editor);
        _intelliSense.Attach(editor, document.Id, document.FilePath, dialect);
    }

    public void Detach(TextEditor editor, TextDocument document)
    {
        _editors.Remove(document.Id);
        _diagnostics.ClearMarkers(editor, document.Id);
        _intelliSense.Detach();
    }

    public void NotifyContentChanged(TextDocument document)
    {
        _analysisHandler.OnDocumentContentChanged(document);
        var dialect = document.FilePath?.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) == true
            ? LuaDialect.Luau
            : LuaDialect.Lua;
        _intelliSense.OnTextChanged(document.Content, document.FilePath, dialect);
    }

    private void OnDocumentAnalyzed(DocumentAnalyzedEvent e)
    {
        if (!_editors.TryGetValue(e.DocumentId, out var editor))
        {
            return;
        }

        _diagnostics.ApplyDiagnostics(editor, e.Result);
    }

    public void Dispose() => _intelliSense.Dispose();
}
