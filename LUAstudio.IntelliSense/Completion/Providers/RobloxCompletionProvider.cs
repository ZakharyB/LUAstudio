using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class RobloxCompletionProvider : ICompletionProvider
{
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _types;

    public RobloxCompletionProvider(IRobloxApiDatabase roblox, ExpressionTypeResolver types)
    {
        _roblox = roblox;
        _types = types;
    }

    public string Name => "roblox";

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<CompletionItem>();

        if (context.TriggerKind is CompletionTriggerKind.Dot or CompletionTriggerKind.Colon)
        {
            var typeName = _types.ResolveCompletionTargetType(context) ?? "Instance";
            foreach (var m in _roblox.GetMembers(typeName, includeInherited: true))
            {
                if (context.TriggerKind == CompletionTriggerKind.Colon &&
                    m.Kind is not (SymbolKind.Method or SymbolKind.Function))
                {
                    continue;
                }

                var insert = m.Kind is SymbolKind.Method or SymbolKind.Function
                    ? m.Name + (m.Name == "GetService" ? "(\"${1:Players}\")" : "()")
                    : m.Name;

                items.Add(new CompletionItem(
                    m.Name,
                    insert,
                    MapKind(m.Kind),
                    m.ReturnType,
                    m.Documentation,
                    priority: 90));
            }

            return items;
        }

        foreach (var (name, typeName) in _roblox.GlobalTypeAliases)
        {
            if (!string.IsNullOrEmpty(context.TriggerPrefix) &&
                !name.StartsWith(context.TriggerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new CompletionItem(name, name, CompletionItemKind.Service, typeName, priority: 95));
        }

        foreach (var service in _roblox.ServiceNames)
        {
            if (!string.IsNullOrEmpty(context.TriggerPrefix) &&
                !service.StartsWith(context.TriggerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new CompletionItem(service, service, CompletionItemKind.Class, service, priority: 70));
        }

        return items;
    }

    private static CompletionItemKind MapKind(SymbolKind kind) => kind switch
    {
        SymbolKind.Method or SymbolKind.Function => CompletionItemKind.Method,
        SymbolKind.Property => CompletionItemKind.Property,
        SymbolKind.Service => CompletionItemKind.Service,
        _ => CompletionItemKind.Field
    };
}
