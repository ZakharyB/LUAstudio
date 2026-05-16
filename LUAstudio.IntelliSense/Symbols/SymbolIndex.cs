using System.Collections.Concurrent;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Symbols;

public sealed class SymbolIndex : ISymbolIndex
{
    private readonly ConcurrentDictionary<Guid, DocumentSymbolTable> _documents = new();
    private readonly ConcurrentDictionary<string, Symbol> _globals = new(StringComparer.Ordinal);
    private readonly IModuleResolver _moduleResolver;

    public SymbolIndex(IModuleResolver moduleResolver) => _moduleResolver = moduleResolver;

    public DocumentSymbolTable? GetDocumentTable(Guid documentId) =>
        _documents.TryGetValue(documentId, out var table) ? table : null;

    public IReadOnlyList<Symbol> GetGlobalSymbols() => _globals.Values.ToArray();

    public Symbol? ResolveRequire(string modulePath, string? fromFilePath) =>
        _moduleResolver.ResolveModule(modulePath, fromFilePath);

    public void UpdateDocument(ParseResult parseResult, SemanticModel semanticModel)
    {
        var snapshot = parseResult.Snapshot;
        var exports = ExtractExports(parseResult);
        var table = new DocumentSymbolTable(
            snapshot.DocumentId,
            snapshot.FilePath,
            semanticModel.RootScope,
            exports)
        {
            Version = snapshot.Version
        };

        _documents[snapshot.DocumentId] = table;

        foreach (var symbol in semanticModel.RootScope.Symbols.Where(s => s.Kind == SymbolKind.Global))
        {
            _globals[symbol.Name] = new Symbol(
                symbol.Name,
                symbol.Kind,
                symbol.DeclarationSpan,
                snapshot.FilePath,
                symbol.Container,
                symbol.Documentation,
                symbol.TypeName);
        }
    }

    public void RemoveDocument(Guid documentId) => _documents.TryRemove(documentId, out _);

    public void InvalidateWorkspace()
    {
        _documents.Clear();
        _globals.Clear();
    }

    private static IReadOnlyList<Symbol> ExtractExports(ParseResult parseResult)
    {
        var exports = new List<Symbol>();
        foreach (var node in parseResult.Tree.Root.DescendantsAndSelf())
        {
            if (node is FunctionDeclarationSyntax { IsLocal: false } fn)
            {
                exports.Add(new Symbol(fn.Name.Text, SymbolKind.Function, fn.Name.Span, parseResult.Snapshot.FilePath));
            }
        }

        return exports;
    }
}
