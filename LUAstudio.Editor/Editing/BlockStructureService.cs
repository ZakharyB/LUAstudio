using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.Editor.Editing;

public sealed record BlockInfo(string Keyword, int HeaderLineEnd, int IndentLevel);

public static class BlockStructureService
{
    public static int TabWidth { get; set; } = 4;

    public static BlockInfo? GetBlockAfterCaret(string text, int caretOffset, SyntaxNode? root)
    {
        var line = GetLineFromOffset(text, caretOffset);
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("function", StringComparison.Ordinal) ||
            trimmed.StartsWith("local function", StringComparison.Ordinal))
        {
            if (!trimmed.Contains("end", StringComparison.Ordinal))
            {
                var indent = line.Length - trimmed.Length;
                return new BlockInfo("function", caretOffset, indent / TabWidth);
            }
        }

        if (trimmed.StartsWith("if ", StringComparison.Ordinal) && trimmed.Contains("then", StringComparison.Ordinal))
        {
            var indent = line.Length - trimmed.Length;
            return new BlockInfo("if", caretOffset, indent / TabWidth);
        }

        if (trimmed.StartsWith("for ", StringComparison.Ordinal) ||
            trimmed.StartsWith("while ", StringComparison.Ordinal))
        {
            var indent = line.Length - trimmed.Length;
            return new BlockInfo("loop", caretOffset, indent / TabWidth);
        }

        return null;
    }

    public static string GetIndent(int level) => new(' ', level * TabWidth);

    private static string GetLineFromOffset(string text, int offset)
    {
        var lineStart = text.LastIndexOf('\n', Math.Min(offset, text.Length - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return text[lineStart..lineEnd];
    }
}
