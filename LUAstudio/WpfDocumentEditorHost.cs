using ICSharpCode.AvalonEdit;
using LUAstudio.Editor.Diagnostics;
using LUAstudio.Editor.Editing;
using LUAstudio.Editor.IntelliSense;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Handlers;
using LUAstudio.IntelliSense.Events;
using LUAstudio.Core.Events;
using LUAstudio.Languages.Text;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio;

public sealed class WpfDocumentEditorHost : IDisposable
{
    private readonly EditorDiagnosticService _diagnostics;
    private readonly DocumentAnalysisHandler _analysisHandler;
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _services;
    private readonly Dictionary<Guid, TextEditor> _editors = new();
    private readonly Dictionary<Guid, EditorFoldingManager> _foldings = new();
    private readonly Dictionary<Guid, EditorDiagnosticHoverController> _diagnosticHovers = new();
    private readonly Dictionary<Guid, EditorIntelliSenseController> _intelliSenseControllers = new();

    public WpfDocumentEditorHost(
        EditorDiagnosticService diagnostics,
        DocumentAnalysisHandler analysisHandler,
        IEventBus eventBus,
        IServiceProvider services)
    {
        _diagnostics = diagnostics;
        _analysisHandler = analysisHandler;
        _eventBus = eventBus;
        _services = services;
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
        if (_diagnosticHovers.Remove(document.Id, out var previousHover))
        {
            previousHover.Detach();
        }

        var hover = new EditorDiagnosticHoverController(_diagnostics);
        hover.Attach(editor, document.Id);
        _diagnosticHovers[document.Id] = hover;

        if (!_foldings.TryGetValue(document.Id, out var folding))
        {
            folding = new EditorFoldingManager();
            _foldings[document.Id] = folding;
        }

        folding.Attach(editor);
        if (_intelliSenseControllers.Remove(document.Id, out var previousController))
        {
            previousController.Dispose();
        }

        var controller = _services.GetRequiredService<EditorIntelliSenseController>();
        controller.Attach(editor, document.Id, document.FilePath, dialect);
        _intelliSenseControllers[document.Id] = controller;
    }

    public void Detach(TextEditor editor, TextDocument document)
    {
        // Ignore a stale Unloaded notification from a view that no longer owns
        // this document. A newly loaded tab may already have replaced it.
        if (!_editors.TryGetValue(document.Id, out var attachedEditor) ||
            !ReferenceEquals(attachedEditor, editor))
        {
            return;
        }

        _editors.Remove(document.Id);
        // AvalonDock unloads tab content while switching documents. Keep the
        // document's latest markers so the squiggles are immediately available
        // when that tab is shown again instead of disappearing until re-analysis.

        if (_foldings.Remove(document.Id, out var folding))
        {
            folding.Detach();
            folding.Dispose();
        }

        if (_intelliSenseControllers.Remove(document.Id, out var controller))
        {
            controller.Dispose();
        }
        if (_diagnosticHovers.Remove(document.Id, out var hover))
        {
            hover.Detach();
        }
    }

    public void NotifyContentChanged(TextDocument document)
    {
        _analysisHandler.OnDocumentContentChanged(document);
        var dialect = document.FilePath?.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) == true
            ? LuaDialect.Luau
            : LuaDialect.Lua;
        if (_intelliSenseControllers.TryGetValue(document.Id, out var controller))
        {
            controller.OnTextChanged(document.Content, document.FilePath, dialect);
        }
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
        foreach (var hover in _diagnosticHovers.Values)
        {
            hover.Detach();
        }

        _diagnosticHovers.Clear();
        foreach (var controller in _intelliSenseControllers.Values)
        {
            controller.Dispose();
        }

        _intelliSenseControllers.Clear();
    }
}
