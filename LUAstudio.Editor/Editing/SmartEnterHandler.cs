using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.Languages.Syntax;

namespace LUAstudio.Editor.Editing;

public sealed class SmartEnterHandler
{
    private TextEditor? _editor;
    private IAnalysisOrchestrator? _analysis;
    private Guid _documentId;

    public void Attach(TextEditor editor, IAnalysisOrchestrator? analysis = null, Guid documentId = default)
    {
        _editor = editor;
        _analysis = analysis;
        _documentId = documentId;
    }

    public void Attach(TextEditor editor) => _editor = editor;

    public void Detach() => _editor = null;

    public bool TryHandleEnter()
    {
        if (_editor is null)
        {
            return false;
        }

        var text = _editor.Document.Text;
        var offset = _editor.CaretOffset;
        SyntaxNode? root = null;
        if (_analysis is not null)
        {
            root = _analysis.GetLatestResult(_documentId)?.ParseResult.Tree.Root;
        }

        var block = BlockStructureService.GetBlockAfterCaret(text, offset, root);
        if (block is null)
        {
            return false;
        }

        var line = GetCurrentLine(text, offset);
        var baseIndent = BlockStructureService.GetIndent(block.IndentLevel);
        var bodyIndent = BlockStructureService.GetIndent(block.IndentLevel + 1);

        var insert = $"\n{bodyIndent}\n{baseIndent}end";
        _editor.Document.Insert(offset, insert);
        _editor.CaretOffset = offset + 1 + bodyIndent.Length;
        return true;
    }

    private static string GetCurrentLine(string text, int offset)
    {
        var lineStart = text.LastIndexOf('\n', Math.Min(Math.Max(0, offset - 1), text.Length - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return text[lineStart..lineEnd];
    }
}
