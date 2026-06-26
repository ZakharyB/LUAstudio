using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class LintRulesAnalyzer : IDiagnosticAnalyzer
{
    private const int LongFunctionLineThreshold = 50;
    private const int DeepNestingThreshold = 4;
    private const int MagicNumberThreshold = 3;

    public int Order => 60;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        CheckLongFunctions(context, diagnostics);
        CheckDeepNesting(context, diagnostics);
        CheckMagicNumbers(context, diagnostics);
        CheckNamingConsistency(context, diagnostics);
        CheckPerformancePatterns(context, diagnostics);
        CheckTableMutationDuringIteration(context, diagnostics);
    }

    private static void CheckLongFunctions(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var fn in context.Binding.Functions)
        {
            var lineCount = CountLines(fn.Node.Span, context.ParseResult.Snapshot.Content);
            if (lineCount > LongFunctionLineThreshold)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2701",
                    $"Function '{fn.Node.Name.Text}' is {lineCount} lines long (threshold: {LongFunctionLineThreshold}).",
                    fn.Node.Name.Span,
                    SemanticDiagnosticSeverity.Info,
                    "Extract smaller functions to improve readability."));
            }
        }
    }

    private static void CheckDeepNesting(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        var depth = 0;
        foreach (var node in context.ParseResult.Tree.Root.DescendantsAndSelf())
        {
            switch (node)
            {
                case IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax:
                    depth++;
                    if (depth >= DeepNestingThreshold)
                    {
                        diagnostics.Add(new SemanticDiagnostic(
                            "LUA2703",
                            $"Deep nesting detected (depth {depth}).",
                            node.Span,
                            SemanticDiagnosticSeverity.Info,
                            "Extract nested logic into helper functions."));
                    }

                    break;

                case FunctionDeclarationSyntax:
                    depth = 0;
                    break;
            }
        }
    }

    private static void CheckMagicNumbers(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var lit in context.ParseResult.Tree.Root.DescendantsAndSelf().OfType<LiteralExpressionSyntax>())
        {
            if (lit.Token.Text is "0" or "1" or "-1" or "2" or "true" or "false" or "nil")
            {
                continue;
            }

            if (double.TryParse(lit.Token.Text, out var num) && Math.Abs(num) > MagicNumberThreshold)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2704",
                    $"Magic number '{lit.Token.Text}' — consider using a named constant.",
                    lit.Span,
                    SemanticDiagnosticSeverity.Hint,
                    $"local CONSTANT = {lit.Token.Text}"));
            }
        }
    }

    private static void CheckNamingConsistency(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        var snake = new HashSet<string>(StringComparer.Ordinal);
        var camel = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (symbol, _) in context.Binding.SymbolUsages)
        {
            if (symbol.Name.Contains('_'))
            {
                snake.Add(symbol.Name);
            }
            else if (char.IsLower(symbol.Name[0]))
            {
                camel.Add(symbol.Name);
            }
        }

        if (snake.Count > 0 && camel.Count > 0)
        {
            var example = camel.First();
            diagnostics.Add(new SemanticDiagnostic(
                "LUA2702",
                "Inconsistent naming: mix of snake_case and camelCase detected.",
                context.ParseResult.Tree.Root.Span,
                SemanticDiagnosticSeverity.Hint,
                "Pick one naming convention (snake_case recommended for Lua)."));
        }
    }

    private static void CheckPerformancePatterns(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        var content = context.ParseResult.Snapshot.Content ?? string.Empty;

        foreach (var loop in context.ParseResult.Tree.Root.DescendantsAndSelf()
                     .Where(n => n is WhileStatementSyntax or ForStatementSyntax))
        {
            var loopText = content.AsSpan(
                Math.Clamp(loop.Span.Start, 0, content.Length),
                Math.Clamp(loop.Span.Length, 0, content.Length - loop.Span.Start)).ToString();

            if (loopText.Contains('{') && loopText.Contains('}'))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2801",
                    "Table literal created inside loop — consider hoisting outside.",
                    loop.Span,
                    SemanticDiagnosticSeverity.Info,
                    "Create the table once before the loop and reuse/clear it."));
            }

            if (loopText.Contains(".."))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2802",
                    "String concatenation in loop — consider table.concat.",
                    loop.Span,
                    SemanticDiagnosticSeverity.Info,
                    "Collect strings in a table and use table.concat(list) after the loop."));
            }
        }
    }

    private static void CheckTableMutationDuringIteration(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var call in context.ParseResult.Tree.Root.DescendantsAndSelf().OfType<CallExpressionSyntax>())
        {
            if (call.Target is not IdentifierNameSyntax { Name.Text: "pairs" or "ipairs" })
            {
                continue;
            }

            var parent = call.Parent;
            while (parent is not null)
            {
                if (parent is ForStatementSyntax forLoop)
                {
                    var bodyText = context.ParseResult.Snapshot.Content ?? string.Empty;
                    var span = forLoop.Body.Span;
                    if (span.Start < bodyText.Length)
                    {
                        var body = bodyText.Substring(span.Start, Math.Min(span.Length, bodyText.Length - span.Start));
                        if (body.Contains('=') && !body.Contains("==") && !body.Contains("~="))
                        {
                            diagnostics.Add(new SemanticDiagnostic(
                                "LUA2503",
                                "Possible table mutation during iteration.",
                                forLoop.Span,
                                SemanticDiagnosticSeverity.Warning,
                                "Collect changes and apply after the loop, or iterate over a copy."));
                        }
                    }

                    break;
                }

                parent = parent.Parent;
            }
        }
    }

    private static int CountLines(LUAstudio.Languages.Text.TextSpan span, string? content)
    {
        if (string.IsNullOrEmpty(content) || span.Length <= 0)
        {
            return 0;
        }

        var start = Math.Clamp(span.Start, 0, content.Length);
        var end = Math.Clamp(span.End, start, content.Length);
        return content.AsSpan(start, end - start).Count('\n') + 1;
    }
}
