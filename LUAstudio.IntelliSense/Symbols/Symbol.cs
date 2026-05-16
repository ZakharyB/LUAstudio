using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Symbols;

public sealed class Symbol
{
    public Symbol(
        string name,
        SymbolKind kind,
        TextSpan declarationSpan,
        string? containingFilePath = null,
        Symbol? container = null,
        string? documentation = null,
        string? typeName = null)
    {
        Name = name;
        Kind = kind;
        DeclarationSpan = declarationSpan;
        ContainingFilePath = containingFilePath;
        Container = container;
        Documentation = documentation;
        TypeName = typeName;
    }

    public string Name { get; }

    public SymbolKind Kind { get; }

    public TextSpan DeclarationSpan { get; }

    public string? ContainingFilePath { get; }

    public Symbol? Container { get; }

    public string? Documentation { get; }

    public string? TypeName { get; }

    public IReadOnlyList<Symbol> Members { get; init; } = Array.Empty<Symbol>();
}
