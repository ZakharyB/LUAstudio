using LUAstudio.IntelliSense.Completion;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Semantic;

public sealed class ExpressionTypeResolver
{
    private readonly IRobloxApiDatabase _roblox;

    public ExpressionTypeResolver(IRobloxApiDatabase roblox) => _roblox = roblox;

    public string? ResolveType(SyntaxNode? node, Scope? scope = null)
    {
        if (node is null)
        {
            return null;
        }

        return node switch
        {
            IdentifierNameSyntax id => ResolveIdentifier(id.Name.Text, scope),
            MemberAccessExpressionSyntax member => ResolveMember(member),
            CallExpressionSyntax call => ResolveCall(call),
            _ => null
        };
    }

    public string? ResolveCompletionTargetType(CompletionContext context)
    {
        var scope = context.SemanticModel?.RootScope;
        var text = context.Snapshot.Content;
        var offset = context.CaretOffset;

        if (offset > 0 && text[offset - 1] is '.' or ':')
        {
            var exprEnd = offset - 1;
            var exprStart = FindExpressionStart(text, exprEnd);
            var exprText = text[exprStart..exprEnd].Trim();

            if (_roblox.GlobalTypeAliases.TryGetValue(exprText, out var alias))
            {
                return alias;
            }

            if (scope is not null && scope.TryResolveLocal(exprText, out var sym) && sym?.TypeName is not null)
            {
                return sym.TypeName;
            }

            return ResolveIdentifier(exprText, scope);
        }

        return context.NodeAtCaret is MemberAccessExpressionSyntax m
            ? ResolveMember(m)
            : ResolveType(context.NodeAtCaret, scope);
    }

    private string? ResolveIdentifier(string name, Scope? scope)
    {
        if (_roblox.GlobalTypeAliases.TryGetValue(name, out var alias))
        {
            return alias;
        }

        if (scope?.TryResolveLocal(name, out var sym) == true && sym?.TypeName is not null)
        {
            return sym.TypeName;
        }

        return _roblox.TryGetGlobal(name, out var g) ? g.ReturnType ?? g.Name : null;
    }

    private string? ResolveMember(MemberAccessExpressionSyntax member)
    {
        var typeName = ResolveType(member.Expression);
        if (typeName is null)
        {
            return null;
        }

        if (_roblox.TryGetMember(typeName, member.Member.Text, out var m))
        {
            return m.ReturnType ?? typeName;
        }

        return typeName;
    }

    private string? ResolveCall(CallExpressionSyntax call)
    {
        if (call.Target is MemberAccessExpressionSyntax member)
        {
            if (member.Member.Text == "GetService" && call.Arguments.Count > 0 &&
                call.Arguments[0] is LiteralExpressionSyntax lit)
            {
                var serviceName = TrimQuotes(lit.Token.Text);
                if (!string.IsNullOrEmpty(serviceName))
                {
                    return serviceName;
                }
            }

            return _roblox.TryGetMember(
                ResolveType(member.Expression) ?? "Instance",
                member.Member.Text,
                out var m)
                ? m.ReturnType
                : null;
        }

        return ResolveType(call.Target);
    }

    private static string TrimQuotes(string text) =>
        text.Trim().Trim('"', '\'');

    private static int FindExpressionStart(string text, int end)
    {
        var depth = 0;
        for (var i = end - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c is ')' or ']' or '}')
            {
                depth++;
            }
            else if (c is '(' or '[' or '{')
            {
                if (depth > 0)
                {
                    depth--;
                }
                else
                {
                    return i + 1;
                }
            }
            else if (depth == 0 && (char.IsWhiteSpace(c) || c is ';' or ',' or '='))
            {
                return i + 1;
            }
        }

        return 0;
    }
}
