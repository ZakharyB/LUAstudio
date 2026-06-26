using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class ModuleDependencyAnalyzer : IDiagnosticAnalyzer
{
    public int Order => 50;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        var filePath = context.FilePath ?? "unknown";
        var seenInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in context.Binding.RequireEdges)
        {
            var resolved = context.ModuleResolver.ResolveModule(edge.ModulePath, filePath);

            if (resolved is null)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2601",
                    $"Missing require target: '{edge.ModulePath}' could not be resolved.",
                    edge.Span,
                    SemanticDiagnosticSeverity.Error,
                    "Check module path or add the file to the workspace."));
                continue;
            }

            seenInFile.TryGetValue(edge.ModulePath, out var count);
            seenInFile[edge.ModulePath] = count + 1;
            if (count > 0)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2603",
                    $"Duplicate require of '{edge.ModulePath}' in the same file.",
                    edge.Span,
                    SemanticDiagnosticSeverity.Info,
                    "Consider caching the required module in a local variable."));
            }

            if (context.RequireGraph.HasCircularDependency(filePath, edge.ModulePath))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2602",
                    $"Circular dependency detected involving '{edge.ModulePath}'.",
                    edge.Span,
                    SemanticDiagnosticSeverity.Error,
                    "Restructure modules to break the dependency cycle."));
            }
        }

        foreach (var dead in context.RequireGraph.GetNodes().Where(n => n.IsDead))
        {
            if (dead.FilePath is null)
            {
                continue;
            }

            // Only report dead modules once per workspace scan — skip per-file to avoid noise
        }
    }
}
