using ICSharpCode.AvalonEdit;
using LUAstudio.Editor.Diagnostics;
using LUAstudio.Editor.Editing;
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
    private readonly EditorDiagnosticHoverController _diagnosticHover;
    private readonly DocumentAnalysisHandler _analysisHandler;
    private readonly IEventBus _eventBus;
    private readonly Dictionary<Guid, TextEditor> _editors = new();
    private readonly Dictionary<Guid, EditorFoldingManager> _foldings = new();

    public WpfDocumentEditorHost(
        EditorIntelliSenseController intelliSense,
        EditorDiagnosticService diagnostics,
        EditorDiagnosticHoverController diagnosticHover,
        DocumentAnalysisHandler analysisHandler,
        IEventBus eventBus)
    {
        _intelliSense = intelliSense;
        _diagnostics = diagnostics;
        _diagnosticHover = diagnosticHover;
        _analysisHandler = analysisHandler;
        _eventBus = eventBus;
        _eventBus.Subscribe<DocumentAnalyzedEvent>(OnDocumentAnalyzed);
    }

    public void Attach(TextEditor editor, TextDocument document)
    {
        var dialect = document.FilePath?.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) == true
            ? LuaDialect.Luau
            : LuaDialect.Lua;

        _editors[document.Id] = editor;
        _diagnostics.Attach(editor, document.Id);
        _diagnostics.RegisterMarkerRenderer(editor);
        _diagnosticHover.Attach(editor, document.Id);

        if (!_foldings.TryGetValue(document.Id, out var folding))
        {
            folding = new EditorFoldingManager();
            _foldings[document.Id] = folding;
        }

        folding.Attach(editor);
        _intelliSense.Attach(editor, document.Id, document.FilePath, dialect);
    }

    public void Detach(TextEditor editor, TextDocument document)
    {
        _editors.Remove(document.Id);
        _diagnostics.ClearMarkers(editor, document.Id);

        if (_foldings.Remove(document.Id, out var folding))
        {
            folding.Detach();
            folding.Dispose();
        }

        _intelliSense.DetachIfEditor(editor);
        _diagnosticHover.Detach();
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

        editor.Dispatcher.Invoke(() => _diagnostics.ApplyDiagnostics(editor, e.Result));
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<DocumentAnalyzedEvent>(OnDocumentAnalyzed);
        foreach (var folding in _foldings.Values)
        {
            folding.Dispose();
        }

        _foldings.Clear();
        _intelliSense.Dispose();
    }
}