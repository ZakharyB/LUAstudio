using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class ScopeSymbolAnalyzer : IDiagnosticAnalyzer
{
    public int Order => 10;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var scope in context.Binding.AllScopes)
        {
            CheckShadowInScope(scope, diagnostics);
        }

        foreach (var (symbol, usage) in context.Binding.SymbolUsages)
        {
            if (usage.ReferenceCount > 0)
            {
                continue;
            }

            if (symbol.Kind is SymbolKind.Parameter)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2002",
                    $"Unused parameter '{symbol.Name}'.",
                    symbol.DeclarationSpan,
                    SemanticDiagnosticSeverity.Hint,
                    $"Prefix with '_' or remove parameter '{symbol.Name}'."));
            }
            else if (symbol.Kind is SymbolKind.Local or SymbolKind.Function)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2003",
                    $"Unused {symbol.Kind.ToString().ToLower()} '{symbol.Name}'.",
                    symbol.DeclarationSpan,
                    SemanticDiagnosticSeverity.Info,
                    $"Remove unused '{symbol.Name}'."));
            }
        }
    }

    private static void CheckShadowInScope(Scope scope, ICollection<SemanticDiagnostic> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in scope.Symbols)
        {
            if (!seen.Add(symbol.Name))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2004",
                    $"Variable '{symbol.Name}' shadows a previous declaration in the same scope.",
                    symbol.DeclarationSpan,
                    SemanticDiagnosticSeverity.Warning,
                    $"Rename '{symbol.Name}' to avoid shadowing."));
            }

            if (scope.Parent?.TryResolveLocal(symbol.Name, out var outer) == true &&
                outer is not null &&
                outer.DeclarationSpan != symbol.DeclarationSpan)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2004",
                    $"Variable '{symbol.Name}' shadows a declaration in an outer scope.",
                    symbol.DeclarationSpan,
                    SemanticDiagnosticSeverity.Warning,
                    $"Rename inner or outer '{symbol.Name}'."));
            }
        }
    }
}
