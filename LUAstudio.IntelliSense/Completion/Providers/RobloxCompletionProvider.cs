using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class RobloxCompletionProvider : ICompletionProvider
{
    private readonly IRobloxApiDatabase _roblox;

    public RobloxCompletionProvider(IRobloxApiDatabase roblox) => _roblox = roblox;

    public string Name => "roblox";

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<CompletionItem>();

        if (context.NodeAtCaret is MemberAccessExpressionSyntax member)
        {
            var typeName = member.Expression switch
            {
                IdentifierNameSyntax id => id.Name.Text,
                MemberAccessExpressionSyntax nested => nested.Member.Text,
                _ => "Instance"
            };

            foreach (var m in _roblox.GetMembers(typeName))
            {
                items.Add(new CompletionItem(
                    m.Name,
                    m.Name,
                    MapKind(m.Kind),
                    m.ReturnType,
                    m.Documentation,
                    priority: 80));
            }

            return items;
        }

        foreach (var key in new[] { "game", "workspace", "script" })
        {
            if (_roblox.TryGetGlobal(key, out var g))
            {
                items.Add(new CompletionItem(g.Name, g.Name, CompletionItemKind.Service, documentation: g.Documentation, priority: 90));
            }
        }

        if (_roblox.TryGetService("Workspace", out _))
        {
            items.Add(new CompletionItem("Workspace", "workspace", CompletionItemKind.Service, priority: 85));
        }

        return items;
    }

    private static CompletionItemKind MapKind(SymbolKind kind) => kind switch
    {
        SymbolKind.Method => CompletionItemKind.Method,
        SymbolKind.Property => CompletionItemKind.Property,
        SymbolKind.Service => CompletionItemKind.Service,
        _ => CompletionItemKind.Field
    };
}
