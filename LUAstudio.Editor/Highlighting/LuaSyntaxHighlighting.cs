using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;
using System.Windows;

namespace LUAstudio.Editor.Highlighting;

/// <summary>
/// Token-driven Lua/Luau syntax highlighter (no regex, fully lexer-backed).
/// </summary>
public sealed class LuaSyntaxHighlighting : DocumentColorizingTransformer
{
    private static readonly HashSet<string> BuiltinFunctions = new(StringComparer.Ordinal)
    {
        "print", "require", "pairs", "ipairs", "next", "typeof", "assert", "error", "pcall", "xpcall",
        "tick", "wait", "spawn", "delay", "warn", "rawget", "rawset", "setmetatable", "getmetatable",
        "select", "unpack", "tonumber", "tostring", "type", "task", "game",
    };

    private readonly IRobloxApiDatabase _roblox;
    private string? _cachedText;
    private IReadOnlyList<LuaToken>? _cachedTokens;

    public LuaSyntaxHighlighting(IRobloxApiDatabase roblox) => _roblox = roblox;

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.Text;
        if (!string.Equals(_cachedText, text, StringComparison.Ordinal))
        {
            _cachedTokens = LuaTokenizer.Tokenize(text);
            _cachedText = text;
        }

        if (_cachedTokens is null)
        {
            return;
        }

        foreach (var token in _cachedTokens)
        {
            if (token.Span.End <= line.Offset || token.Span.Start >= line.EndOffset)
            {
                continue;
            }

            if (token.Kind == LuaTokenKind.Comment)
            {
                ColorizeSpan(token.Span, HighlightBrushes.Comment, line);
                ColorizeTodoInComment(token, line);
                continue;
            }

            var brush = GetBrush(token);
            if (brush is null)
            {
                continue;
            }

            ColorizeSpan(token.Span, brush, line);
        }
    }

    private void ColorizeTodoInComment(LuaToken token, DocumentLine line)
    {
        const string marker = "TODO";
        var index = token.Text.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var start = token.Span.Start + index;
            var end = start + marker.Length;
            if (end > line.Offset && start < line.EndOffset)
            {
                ColorizeSpan(TextSpan.FromBounds(start, end), HighlightBrushes.Todo, line, bold: true);
            }

            index = token.Text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
        }
    }

    private SolidColorBrush? GetBrush(LuaToken token)
    {
        return token.Kind switch
        {
            LuaTokenKind.Keyword when token.Keyword is "nil" or "true" or "false" => HighlightBrushes.Bool,
            LuaTokenKind.Keyword => HighlightBrushes.Keyword,

            LuaTokenKind.String => HighlightBrushes.String,

            LuaTokenKind.Number => HighlightBrushes.Number,

            LuaTokenKind.Operator => HighlightBrushes.Operator,

            LuaTokenKind.Punctuation => HighlightBrushes.Bracket,

            LuaTokenKind.Identifier when token.Text == "self" => HighlightBrushes.Keyword,

            LuaTokenKind.Identifier when _roblox.GlobalTypeAliases.ContainsKey(token.Text) => HighlightBrushes.Information,

            LuaTokenKind.Identifier when _roblox.TryGetGlobal(token.Text, out _) => HighlightBrushes.Information,

            LuaTokenKind.Identifier when BuiltinFunctions.Contains(token.Text) => HighlightBrushes.Builtin,

            LuaTokenKind.Identifier => HighlightBrushes.Text,

            _ => null
        };
    }

    private void ColorizeSpan(TextSpan span, SolidColorBrush brush, DocumentLine line, bool bold = false)
    {
        var start = Math.Max(span.Start, line.Offset);
        var end = Math.Min(span.End, line.EndOffset);

        if (start >= end)
        {
            return;
        }

        ChangeLinePart(
            start,
            end,
            element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
                if (bold)
                {
                    element.TextRunProperties.SetTypeface(new Typeface(
                        element.TextRunProperties.Typeface.FontFamily,
                        FontStyles.Normal,
                        FontWeights.Bold,
                        FontStretches.Normal));
                }
            });
    }
}
