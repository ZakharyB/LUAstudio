namespace LUAstudio.IntelliSense.Symbols;

public sealed class Scope
{
    public Scope(Scope? parent = null)
    {
        Parent = parent;
    }

    public Scope? Parent { get; }

    public Dictionary<string, Symbol> Locals { get; } = new(StringComparer.Ordinal);

    public List<Symbol> Symbols { get; } = [];

    public bool TryResolveLocal(string name, out Symbol? symbol)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope.Locals.TryGetValue(name, out symbol))
            {
                return true;
            }
        }

        symbol = null;
        return false;
    }

    public IEnumerable<Symbol> EnumerateAccessibleSymbols()
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            foreach (var symbol in scope.Locals.Values)
            {
                yield return symbol;
            }
        }
    }
}
