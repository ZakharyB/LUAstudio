using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.Editor.Highlighting;

public sealed class SemanticHighlightingClassifier : DocumentColorizingTransformer
{
    private static readonly SolidColorBrush KeywordBrush = Freeze(0xC5, 0x86, 0xC8);
    private static readonly SolidColorBrush GlobalBrush = Freeze(0x56, 0x9C, 0xD6);
    private static readonly SolidColorBrush MethodBrush = Freeze(0x4E, 0xC9, 0xB0);
    private static readonly SolidColorBrush PropertyBrush = Freeze(0x56, 0x9C, 0xD6);
    private static readonly SolidColorBrush TypeBrush = Freeze(0x4E, 0xC9, 0xB0);

    private static readonly Regex MethodAfterDotOrColonRegex = new(
        @"(?<=[.:])([A-Za-z_][\w]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PropertyAfterDotRegex = new(
        @"\.([A-Za-z_][\w]*)",
        RegexOptions.Compiled);


    // idk if i need to remove it or not but if it work keep it
    private static readonly HashSet<string> RobloxMethods = new(StringComparer.Ordinal)
    {
        "GetService", "FindFirstChild", "WaitForChild", "GetChildren", "GetDescendants",
        "IsA", "Clone", "Destroy", "Connect", "Once", "Fire", "Invoke"
    };

    private readonly IAnalysisOrchestrator _analysis;
    private readonly IRobloxApiDatabase _roblox;
    private Guid _documentId;

    public SemanticHighlightingClassifier(IAnalysisOrchestrator analysis, IRobloxApiDatabase roblox)
    {
        _analysis = analysis;
        _roblox = roblox;
    }

    public void SetDocument(Guid documentId) => _documentId = documentId;

    public void Invalidate() { }

    protected override void ColorizeLine(DocumentLine line)
    {
        ColorizeMemberAccessFromText(line);

        var result = _analysis.GetLatestResult(_documentId);
        if (result is null)
        {
            return;
        }

        foreach (var node in result.ParseResult.Tree.Root.DescendantsAndSelf())
        {
            if (node.Span.Start >= line.EndOffset || node.Span.End <= line.Offset)
            {
                continue;
            }

            switch (node)
            {
                case FunctionDeclarationSyntax fn:
                    ColorizeSpan(fn.Name.Span, KeywordBrush, line);
                    break;

                case IdentifierNameSyntax id when _roblox.GlobalTypeAliases.ContainsKey(id.Name.Text):
                    ColorizeSpan(id.Span, GlobalBrush, line);
                    break;

                case CallExpressionSyntax call when call.Target is MemberAccessExpressionSyntax member:
                    ColorizeSpan(member.Member.Span, MethodBrush, line);
                    break;

                case MemberAccessExpressionSyntax member when member.Member.Text != "[]":
                    if (member.Parent is not CallExpressionSyntax parentCall || parentCall.Target != member)
                    {
                        ColorizeSpan(member.Member.Span, PropertyBrush, line);
                    }

                    break;

                case TypeAnnotationSyntax typeAnn:
                    ColorizeSpan(typeAnn.Span, TypeBrush, line);
                    break;
            }
        }
    }

    private void ColorizeMemberAccessFromText(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;
        var methodSpans = new List<(int Start, int End)>();

        foreach (Match match in MethodAfterDotOrColonRegex.Matches(text))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            methodSpans.Add((start, end));
            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(MethodBrush));
        }

        foreach (Match match in PropertyAfterDotRegex.Matches(text))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            if (methodSpans.Any(s => s.Start <= start && end <= s.End))
            {
                continue;
            }

            var afterMatch = match.Index + match.Length;
            if (afterMatch < text.Length && text[afterMatch..].TrimStart().StartsWith('('))
            {
                continue;
            }

            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(PropertyBrush));
        }
    }

    private void ColorizeSpan(TextSpan span, SolidColorBrush brush, DocumentLine line)
    {
        var start = Math.Max(span.Start, line.Offset);
        var end = Math.Min(span.End, line.EndOffset);
        if (start < end)
        {
            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
