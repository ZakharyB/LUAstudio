using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense;

public sealed class HoverInfoService
{
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _types;
    private readonly ISymbolIndex _symbols;

    public HoverInfoService(IRobloxApiDatabase roblox, ExpressionTypeResolver types, ISymbolIndex symbols)
    {
        _roblox = roblox;
        _types = types;
        _symbols = symbols;
    }

    public async Task<string?> GetHoverAsync(Completion.CompletionContext context, CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (context.NodeAtCaret is IdentifierNameSyntax id)
        {
            var scope = context.SemanticModel?.RootScope;
            if (scope?.TryResolveLocal(id.Name.Text, out var local) == true && local is not null)
                return FormatSymbol(local);

            var indexed = _symbols.GetDocumentTable(context.Snapshot.DocumentId)?.RootScope.Symbols
                .LastOrDefault(symbol => symbol.Name == id.Name.Text && symbol.DeclarationSpan.Start <= context.CaretOffset)
                ?? _symbols.GetGlobalSymbols().FirstOrDefault(symbol => symbol.Name == id.Name.Text);
            if (indexed is not null)
                return FormatSymbol(indexed);

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
            var typeName = _types.ResolveType(member.Expression, context.SemanticModel?.RootScope);
            if (typeName is not null && _roblox.TryGetMember(typeName, member.Member.Text, out var m))
            {
                return Format(m.Documentation, m.ReturnType);
            }
        }

        return null;
    }

    private static string? Format(string? doc, string? type) =>
        type is not null ? $"`{type}`\n\n{doc}" : doc;

    private static string FormatSymbol(Symbol symbol)
    {
        var signature = symbol.TypeName is null ? symbol.Kind.ToString().ToLowerInvariant() : symbol.TypeName;
        return $"**{symbol.Name}** : `{signature}`{(string.IsNullOrWhiteSpace(symbol.Documentation) ? string.Empty : $"\n\n{symbol.Documentation}")}";
    }
}
