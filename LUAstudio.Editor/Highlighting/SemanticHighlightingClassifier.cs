using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.Languages.Text;

namespace LUAstudio.Editor.Highlighting;

public sealed class SemanticHighlightingClassifier : DocumentColorizingTransformer
{
    private static readonly Regex MethodAfterDotOrColonRegex = new(
        @"(?<=[.:])([A-Za-z_][\w]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PropertyAfterDotRegex = new(
        @"\.([A-Za-z_][\w]*)",
        RegexOptions.Compiled);

    private static readonly Regex BaseBeforeDotRegex = new(
        @"(?<![.\w])([A-Za-z_][\w]*)(?=\s*\.)",
        RegexOptions.Compiled);

    private static readonly Regex FunctionNameRegex = new(
        @"\bfunction\s+([A-Za-z_][\w]*)",
        RegexOptions.Compiled);

    private static readonly Regex LocalFunctionNameRegex = new(
        @"\blocal\s+function\s+([A-Za-z_][\w]*)",
        RegexOptions.Compiled);

    private readonly IAnalysisOrchestrator _analysis;
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _typeResolver;
    private Guid _documentId;
    private string? _cachedText;
    private IReadOnlyList<LuaToken>? _cachedTokens;

    public SemanticHighlightingClassifier(
        IAnalysisOrchestrator analysis,
        IRobloxApiDatabase roblox,
        ExpressionTypeResolver typeResolver)
    {
        _analysis = analysis;
        _roblox = roblox;
        _typeResolver = typeResolver;
    }

    public void SetDocument(Guid documentId) => _documentId = documentId;

    public void Invalidate() { }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Engine.Globals.Get<bool>(SettingKeys.EditorSemanticHighlighting)?.Value == false)
        {
            return;
        }

        var tokens = GetTokens();
        var result = _analysis.GetLatestResult(_documentId);
        var scope = result?.SemanticModel.RootScope;

        if (result is not null)
        {
            foreach (var node in result.ParseResult.Tree.Root.DescendantsAndSelf())
            {
                if (node.Span.Start >= line.EndOffset || node.Span.End <= line.Offset)
                {
                    continue;
                }

                switch (node)
                {
                    case FunctionDeclarationSyntax fn:
                        ColorizeSpan(fn.Name.Span, HighlightBrushes.FunctionName, line, tokens);
                        break;

                    case LocalStatementSyntax local:
                        ColorizeSpan(local.Name.Span, HighlightBrushes.LocalProperty, line, tokens);
                        break;

                    case ParameterSyntax parameter:
                        ColorizeSpan(parameter.Name.Span, HighlightBrushes.LocalProperty, line, tokens);
                        break;

                    case MemberAccessExpressionSyntax memberAccess:
                        ColorizeMemberAccess(memberAccess, line, tokens, scope);
                        break;

                    case IdentifierNameSyntax id
                        when (_roblox.GlobalTypeAliases.ContainsKey(id.Name.Text) ||
                              _roblox.TryGetGlobal(id.Name.Text, out _))
                             && (id.Parent is not MemberAccessExpressionSyntax parent || parent.Expression != id):
                        ColorizeSpan(id.Name.Span, HighlightBrushes.Information, line, tokens);
                        break;

                    case TypeAnnotationSyntax typeAnn:
                        ColorizeSpan(typeAnn.TypeName.Span, HighlightBrushes.Type, line, tokens);
                        break;
                }
            }
        }
        else
        {
            ColorizeFromTextFallback(line, tokens);
        }
    }

    private void ColorizeMemberAccess(
        MemberAccessExpressionSyntax member,
        DocumentLine line,
        IReadOnlyList<LuaToken> tokens,
        Scope? scope)
    {
        if (member.Expression is IdentifierNameSyntax baseId)
        {
            var baseBrush = _roblox.GlobalTypeAliases.ContainsKey(baseId.Name.Text) ||
                            _roblox.TryGetGlobal(baseId.Name.Text, out _)
                ? HighlightBrushes.Information
                : HighlightBrushes.Text;
            ColorizeSpan(baseId.Name.Span, baseBrush, line, tokens);
        }

        var ownerType = _typeResolver.ResolveType(member.Expression, scope);
        if (ownerType is not null &&
            _roblox.TryGetMember(ownerType, member.Member.Text, out var robloxMember))
        {
            var brush = robloxMember.Kind is SymbolKind.Method or SymbolKind.Function
                ? HighlightBrushes.Builtin
                : HighlightBrushes.Information;
            ColorizeSpan(member.Member.Span, brush, line, tokens);
            return;
        }

        var isCall = member.Parent is CallExpressionSyntax call && call.Target == member;
        ColorizeSpan(
            member.Member.Span,
            isCall ? HighlightBrushes.LocalMethod : HighlightBrushes.LocalProperty,
            line,
            tokens);
    }

    private void ColorizeFromTextFallback(DocumentLine line, IReadOnlyList<LuaToken> tokens)
    {
        var text = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;

        foreach (Match match in LocalFunctionNameRegex.Matches(text).Concat(FunctionNameRegex.Matches(text)))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            if (!OverlapsComment(start, end, tokens))
            {
                ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(HighlightBrushes.FunctionName));
            }
        }

        ColorizeMemberAccessFromText(line, tokens, text, lineStart);
    }

    private void ColorizeMemberAccessFromText(
        DocumentLine line,
        IReadOnlyList<LuaToken> tokens,
        string? text = null,
        int lineStart = 0)
    {
        text ??= CurrentContext.Document.GetText(line);
        lineStart = lineStart == 0 ? line.Offset : lineStart;
        var methodSpans = new List<(int Start, int End)>();

        foreach (Match match in BaseBeforeDotRegex.Matches(text))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            if (OverlapsComment(start, end, tokens))
            {
                continue;
            }

            var brush = _roblox.GlobalTypeAliases.ContainsKey(nameGroup.Value) ||
                        _roblox.TryGetGlobal(nameGroup.Value, out _)
                ? HighlightBrushes.Information
                : HighlightBrushes.Text;
            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(brush));
        }

        foreach (Match match in MethodAfterDotOrColonRegex.Matches(text))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            if (OverlapsComment(start, end, tokens))
            {
                continue;
            }

            methodSpans.Add((start, end));
            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(HighlightBrushes.LocalMethod));
        }

        foreach (Match match in PropertyAfterDotRegex.Matches(text))
        {
            var nameGroup = match.Groups[1];
            var start = lineStart + nameGroup.Index;
            var end = start + nameGroup.Length;
            if (OverlapsComment(start, end, tokens))
            {
                continue;
            }

            if (methodSpans.Any(s => s.Start <= start && end <= s.End))
            {
                continue;
            }

            var afterMatch = match.Index + match.Length;
            if (afterMatch < text.Length && text[afterMatch..].TrimStart().StartsWith('('))
            {
                continue;
            }

            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(HighlightBrushes.LocalProperty));
        }
    }

    private void ColorizeSpan(TextSpan span, SolidColorBrush brush, DocumentLine line, IReadOnlyList<LuaToken> tokens)
    {
        var start = Math.Max(span.Start, line.Offset);
        var end = Math.Min(span.End, line.EndOffset);
        if (start < end && !OverlapsComment(start, end, tokens))
        {
            ChangeLinePart(start, end, el => el.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private IReadOnlyList<LuaToken> GetTokens()
    {
        var text = CurrentContext.Document.Text;
        if (!string.Equals(_cachedText, text, StringComparison.Ordinal))
        {
            _cachedTokens = LuaTokenizer.Tokenize(text);
            _cachedText = text;
        }

        return _cachedTokens ?? Array.Empty<LuaToken>();
    }

    private static bool OverlapsComment(int start, int end, IReadOnlyList<LuaToken> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Kind != LuaTokenKind.Comment)
            {
                continue;
            }

            if (token.Span.Start < end && start < token.Span.End)
            {
                return true;
            }
        }

        return false;
    }
}
