using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace LUAstudio.Editor.Highlighting;

/// <summary>
/// Lightweight Lua/Luau syntax colorizer for AvalonEdit without loading full XSHD definitions.
/// </summary>
public sealed class LuaSyntaxHighlighting : DocumentColorizingTransformer
{
    private static readonly SolidColorBrush KeywordBrush = Freeze(Color.FromRgb(0xC5, 0x86, 0xC8));
    private static readonly SolidColorBrush StringBrush = Freeze(Color.FromRgb(0xCE, 0x91, 0x78));
    private static readonly SolidColorBrush NumberBrush = Freeze(Color.FromRgb(0xB5, 0xCE, 0xA8));
    private static readonly SolidColorBrush CommentBrush = Freeze(Color.FromRgb(0x6A, 0x99, 0x55));
    private static readonly SolidColorBrush BuiltinBrush = Freeze(Color.FromRgb(0x4E, 0xC9, 0xB0));

    private static readonly Regex KeywordRegex = new(
        @"\b(and|break|do|else|elseif|end|false|for|function|goto|if|in|local|nil|not|or|repeat|return|then|true|until|while|type|export)\b",
        RegexOptions.Compiled);

    private static readonly Regex StringRegex = new(@"('[^'\\]*(?:\\.[^'\\]*)*'|""[^""\\]*(?:\\.[^""\\]*)*"")", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\b\d+(?:\.\d+)?\b", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new(@"--[^\n]*", RegexOptions.Compiled);
    private static readonly Regex BuiltinRegex = new(@"\b(print|require|pairs|ipairs|typeof|game|workspace)\b", RegexOptions.Compiled);

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        Apply(KeywordRegex, KeywordBrush, line, text);
        Apply(StringRegex, StringBrush, line, text);
        Apply(NumberRegex, NumberBrush, line, text);
        Apply(CommentRegex, CommentBrush, line, text);
        Apply(BuiltinRegex, BuiltinBrush, line, text);
    }

    private void Apply(Regex regex, SolidColorBrush brush, DocumentLine line, string text)
    {
        foreach (Match match in regex.Matches(text))
        {
            ChangeLinePart(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
