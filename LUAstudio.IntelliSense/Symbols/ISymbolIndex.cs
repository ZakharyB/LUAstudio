using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Symbols;

public interface ISymbolIndex
{
    DocumentSymbolTable? GetDocumentTable(Guid documentId);

    IReadOnlyList<Symbol> GetGlobalSymbols();

    Symbol? ResolveRequire(string modulePath, string? fromFilePath);

    void UpdateDocument(ParseResult parseResult, SemanticModel semanticModel);

    void RemoveDocument(Guid documentId);

    void InvalidateWorkspace();
}

public sealed class DocumentSymbolTable
{
    public DocumentSymbolTable(Guid documentId, string? filePath, Scope rootScope, IReadOnlyList<Symbol> exports)
    {
        DocumentId = documentId;
        FilePath = filePath;
        RootScope = rootScope;
        Exports = exports;
    }

    public Guid DocumentId { get; }

    public string? FilePath { get; }

    public Scope RootScope { get; }

    public IReadOnlyList<Symbol> Exports { get; }

    public int Version { get; init; }
}
