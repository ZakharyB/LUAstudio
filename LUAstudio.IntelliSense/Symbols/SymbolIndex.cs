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
    private IReadOnlyList<Symbol>? _cachedGlobals;
    private readonly object _globalsLock = new();

    public SymbolIndex(IModuleResolver moduleResolver) => _moduleResolver = moduleResolver;

    public DocumentSymbolTable? GetDocumentTable(Guid documentId) =>
        _documents.TryGetValue(documentId, out var table) ? table : null;

    public IReadOnlyList<Symbol> GetGlobalSymbols()
    {
        if (_cachedGlobals is not null)
        {
            return _cachedGlobals;
        }

        lock (_globalsLock)
        {
            if (_cachedGlobals is not null)
            {
                return _cachedGlobals;
            }

            _cachedGlobals = _globals.Values.ToArray();
            return _cachedGlobals;
        }
    }

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

        var globalsChanged = false;
        foreach (var symbol in semanticModel.RootScope.Symbols.Where(s => s.Kind == SymbolKind.Global))
        {
            var newSymbol = new Symbol(
                symbol.Name,
                symbol.Kind,
                symbol.DeclarationSpan,
                snapshot.FilePath,
                symbol.Container,
                symbol.Documentation,
                symbol.TypeName);

            if (!_globals.TryGetValue(symbol.Name, out var existing) ||
                !SymbolEquals(existing, newSymbol))
            {
                _globals[symbol.Name] = newSymbol;
                globalsChanged = true;
            }
        }

        if (globalsChanged)
        {
            lock (_globalsLock)
            {
                _cachedGlobals = null;
            }
        }
    }

    private static bool SymbolEquals(Symbol a, Symbol b) =>
        a.Name == b.Name &&
        a.Kind == b.Kind &&
        a.DeclarationSpan == b.DeclarationSpan &&
        a.FilePath == b.FilePath &&
        a.TypeName == b.TypeName;

    public void RemoveDocument(Guid documentId) => _documents.TryRemove(documentId, out _);

    public void InvalidateWorkspace()
    {
        _documents.Clear();
        _globals.Clear();
        lock (_globalsLock)
        {
            _cachedGlobals = null;
        }
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
