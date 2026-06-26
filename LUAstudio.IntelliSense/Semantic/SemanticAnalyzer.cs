using LUAstudio.IntelliSense.Diagnostics;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Parsing;

namespace LUAstudio.IntelliSense.Semantic;

public sealed class SemanticAnalyzer
{
    private readonly SemanticBinder _binder = new();
    private readonly DiagnosticEngine _diagnostics;
    private readonly IRobloxApiDatabase _roblox;
    private readonly ISymbolIndex _symbolIndex;
    private readonly IModuleResolver _moduleResolver;
    private readonly RequireGraphService _requireGraph;

    public SemanticAnalyzer(
        DiagnosticEngine diagnostics,
        IRobloxApiDatabase roblox,
        ISymbolIndex symbolIndex,
        IModuleResolver moduleResolver,
        RequireGraphService requireGraph)
    {
        _diagnostics = diagnostics;
        _roblox = roblox;
        _symbolIndex = symbolIndex;
        _moduleResolver = moduleResolver;
        _requireGraph = requireGraph;
    }

    public SemanticModel Analyze(ParseResult parseResult)
    {
        var binding = _binder.Bind(parseResult);
        UpdateRequireGraph(parseResult, binding);

        var ctx = new DiagnosticAnalysisContext(
            parseResult,
            binding,
            _roblox,
            _symbolIndex,
            _moduleResolver,
            _requireGraph);

        var diagnostics = _diagnostics.Analyze(ctx);
        return new SemanticModel(parseResult, binding.RootScope, diagnostics, binding.InferredTypes, binding);
    }

    private void UpdateRequireGraph(ParseResult parseResult, SemanticBindingResult binding)
    {
        var filePath = parseResult.Snapshot.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var edges = binding.RequireEdges
            .Select(edge =>
            {
                var resolved = _moduleResolver.ResolveModule(edge.ModulePath, filePath);
                return (edge.ModulePath, resolved?.ContainingFilePath);
            })
            .ToArray();

        _requireGraph.SetFileRequires(filePath, edges);
    }
}
