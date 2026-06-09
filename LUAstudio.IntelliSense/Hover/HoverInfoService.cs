using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense;

public sealed class HoverInfoService
{
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _types;

    public HoverInfoService(IRobloxApiDatabase roblox, ExpressionTypeResolver types)
    {
        _roblox = roblox;
        _types = types;
    }

    public async Task<string?> GetHoverAsync(Completion.CompletionContext context, CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (context.NodeAtCaret is IdentifierNameSyntax id)
        {
            if (_roblox.TryGetGlobal(id.Name.Text, out var g))
            {
                return Format(g.Documentation, g.ReturnType);
            }

            if (_roblox.GlobalTypeAliases.TryGetValue(id.Name.Text, out var typeName))
            {
                return $"**{id.Name.Text}** : `{typeName}`";
            }
        }

        if (context.NodeAtCaret is MemberAccessExpressionSyntax member)
        {
            var typeName = _types.ResolveType(member.Expression);
            if (typeName is not null && _roblox.TryGetMember(typeName, member.Member.Text, out var m))
            {
                return Format(m.Documentation, m.ReturnType);
            }
        }

        return null;
    }

    private static string? Format(string? doc, string? type) =>
        type is not null ? $"`{type}`\n\n{doc}" : doc;
}
