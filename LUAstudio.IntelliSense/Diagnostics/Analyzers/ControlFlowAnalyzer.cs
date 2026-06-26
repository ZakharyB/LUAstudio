using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class ControlFlowAnalyzer : IDiagnosticAnalyzer
{
    public int Order => 20;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var node in context.ParseResult.Tree.Root.DescendantsAndSelf())
        {
            if (node is BlockSyntax block)
            {
                CheckUnreachableInBlock(block, diagnostics);
            }

            if (node is WhileStatementSyntax whileLoop)
            {
                CheckInfiniteLoop(whileLoop, diagnostics);
            }
        }

        CheckBreakContinueMisuse(context, diagnostics);
        CheckCoroutineYieldMisuse(context, diagnostics);
    }

    private static void CheckUnreachableInBlock(BlockSyntax block, ICollection<SemanticDiagnostic> diagnostics)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count - 1; i++)
        {
            if (IsUnconditionalExit(statements[i]))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2005",
                    "Unreachable code detected after unconditional exit.",
                    statements[i + 1].Span,
                    SemanticDiagnosticSeverity.Warning,
                    "Remove unreachable code or restructure control flow."));
            }
        }
    }

    private static void CheckInfiniteLoop(WhileStatementSyntax whileLoop, ICollection<SemanticDiagnostic> diagnostics)
    {
        if (whileLoop.Condition is LiteralExpressionSyntax { Token.Text: "true" })
        {
            var hasBreak = whileLoop.Body.DescendantsAndSelf()
                .Any(n => n is LiteralExpressionSyntax { Token.Text: "break" or "return" });
            if (!hasBreak)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2101",
                    "Possible infinite loop: while true with no break or return.",
                    whileLoop.Span,
                    SemanticDiagnosticSeverity.Warning,
                    "Add a break condition or use a bounded loop."));
            }
        }
    }

    private static void CheckBreakContinueMisuse(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        var loopDepth = 0;
        foreach (var node in context.ParseResult.Tree.Root.DescendantsAndSelf())
        {
            switch (node)
            {
                case WhileStatementSyntax or ForStatementSyntax:
                    loopDepth++;
                    break;

                case FunctionDeclarationSyntax:
                    loopDepth = 0;
                    break;

                case LiteralExpressionSyntax { Token.Text: "break" or "continue" } lit when loopDepth == 0:
                    diagnostics.Add(new SemanticDiagnostic(
                        "LUA2102",
                        $"'{lit.Token.Text}' used outside of a loop.",
                        lit.Span,
                        SemanticDiagnosticSeverity.Error,
                        $"Remove '{lit.Token.Text}' or wrap in a loop."));
                    break;
            }
        }
    }

    private static void CheckCoroutineYieldMisuse(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var call in context.ParseResult.Tree.Root.DescendantsAndSelf().OfType<CallExpressionSyntax>())
        {
            if (call.Target is not MemberAccessExpressionSyntax member ||
                member.Member.Text != "yield")
            {
                continue;
            }

            if (member.Expression is IdentifierNameSyntax { Name.Text: "coroutine" })
            {
                continue;
            }

            diagnostics.Add(new SemanticDiagnostic(
                "LUA2103",
                "coroutine.yield() should only be called from coroutine context.",
                call.Span,
                SemanticDiagnosticSeverity.Warning,
                "Ensure this runs inside a coroutine created with coroutine.create or coroutine.wrap."));
        }
    }

    private static bool IsUnconditionalExit(SyntaxNode node) =>
        node is LiteralExpressionSyntax { Token.Text: "return" or "break" };
}
