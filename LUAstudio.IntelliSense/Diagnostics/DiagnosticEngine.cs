using LUAstudio.IntelliSense.Semantic;

namespace LUAstudio.IntelliSense.Diagnostics;

public sealed class DiagnosticEngine
{
    private readonly IReadOnlyList<IDiagnosticAnalyzer> _analyzers;

    public DiagnosticEngine(IEnumerable<IDiagnosticAnalyzer> analyzers) =>
        _analyzers = analyzers.OrderBy(a => a.Order).ToArray();

    public IReadOnlyList<SemanticDiagnostic> Analyze(DiagnosticAnalysisContext context)
    {
        if (!context.Enabled)
        {
            return Array.Empty<SemanticDiagnostic>();
        }

        var diagnostics = new List<SemanticDiagnostic>();
        foreach (var analyzer in _analyzers)
        {
            analyzer.Analyze(context, diagnostics);
        }

        return diagnostics;
    }
}
