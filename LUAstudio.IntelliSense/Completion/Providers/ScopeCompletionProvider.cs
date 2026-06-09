using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class ScopeCompletionProvider : ICompletionProvider
{
    private readonly ISymbolIndex _symbols;

    public ScopeCompletionProvider(ISymbolIndex symbols) => _symbols = symbols;

    public string Name => "scope";

    public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var items = new List<CompletionItem>();
        var table = _symbols.GetDocumentTable(context.Snapshot.DocumentId);
        if (table is null)
        {
            return Task.FromResult<IReadOnlyList<CompletionItem>>(items);
        }

        foreach (var symbol in table.RootScope.EnumerateAccessibleSymbols())
        {
            items.Add(new CompletionItem(
                symbol.Name,
                symbol.Name,
                MapKind(symbol.Kind),
                symbol.TypeName,
                symbol.Documentation,
                priority: 100));
        }

        if (context.NodeAtCaret is MemberAccessExpressionSyntax member)
        {
            items.Clear();
            var typeName = member.Expression switch
            {
                IdentifierNameSyntax id => id.Name.Text,
                _ => null
            };

            if (typeName is not null)
            {
                foreach (var global in _symbols.GetGlobalSymbols().Where(s => s.Name == typeName))
                {
                    foreach (var child in global.Members)
                    {
                        items.Add(new CompletionItem(child.Name, child.Name, MapKind(child.Kind), child.TypeName));
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<CompletionItem>>(items);
    }

    private static CompletionItemKind MapKind(SymbolKind kind) => kind switch
    {
        SymbolKind.Function or SymbolKind.Method => CompletionItemKind.Function,
        SymbolKind.Module => CompletionItemKind.Module,
        SymbolKind.Class or SymbolKind.Service => CompletionItemKind.Class,
        SymbolKind.Property or SymbolKind.Field => CompletionItemKind.Property,
        _ => CompletionItemKind.Variable
    };
}
