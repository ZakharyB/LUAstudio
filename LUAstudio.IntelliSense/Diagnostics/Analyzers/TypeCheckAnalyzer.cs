using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class TypeCheckAnalyzer : IDiagnosticAnalyzer
{
    public int Order => 40;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var fn in context.Binding.Functions)
        {
            CheckReturnPaths(fn, context, diagnostics);
            CheckReturnTypes(fn, context, diagnostics);
        }

        foreach (var (name, type) in context.Binding.InferredTypes)
        {
            if (type.TableShape is not null && type.TableShape.Count > 0)
            {
                // Table shape inferred — informational for strict mode signature checks later.
            }
        }

        CheckUnsafeIndexing(context, diagnostics);
        CheckNilDereference(context, diagnostics);
    }

    private static void CheckReturnPaths(BoundFunction fn, DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        if (fn.DeclaredReturnType is null || fn.DeclaredReturnType.IsUnknown)
        {
            return;
        }

        var paths = AnalyzePaths(fn.Node.Body.Block);
        if (!paths.AllPathsReturn)
        {
            diagnostics.Add(new SemanticDiagnostic(
                "LUA2401",
                $"Function '{fn.Node.Name.Text}' may not return on all code paths.",
                fn.Node.Name.Span,
                context.StrictMode ? SemanticDiagnosticSeverity.Error : SemanticDiagnosticSeverity.Warning,
                "Add return statements to all branches or return nil explicitly."));
        }
    }

    private static void CheckReturnTypes(BoundFunction fn, DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        if (fn.DeclaredReturnType is null || fn.DeclaredReturnType.IsUnknown)
        {
            return;
        }

        var returnTypes = CollectReturnExpressionTypes(fn.Node.Body.Block);
        foreach (var (type, span) in returnTypes)
        {
            if (!fn.DeclaredReturnType.IsCompatibleWith(type))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2402",
                    $"Return type '{type}' is inconsistent with declared return type '{fn.DeclaredReturnType}'.",
                    span,
                    context.StrictMode ? SemanticDiagnosticSeverity.Error : SemanticDiagnosticSeverity.Warning,
                    $"Cast or change return to match '{fn.DeclaredReturnType}'."));
            }
        }
    }

    private static void CheckUnsafeIndexing(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var member in context.ParseResult.Tree.Root.DescendantsAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (member.Expression is not IdentifierNameSyntax id)
            {
                continue;
            }

            if (context.Binding.InferredTypes.TryGetValue(id.Name.Text, out var type) && type.MightBeNil)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2502",
                    $"Unsafe indexing: '{id.Name.Text}' may be nil.",
                    member.Member.Span,
                    SemanticDiagnosticSeverity.Warning,
                    $"Guard '{id.Name.Text}' against nil before accessing '.{member.Member.Text}'."));
            }
        }
    }

    private static void CheckNilDereference(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var call in context.ParseResult.Tree.Root.DescendantsAndSelf().OfType<CallExpressionSyntax>())
        {
            if (call.Target is not MemberAccessExpressionSyntax member ||
                member.Expression is not IdentifierNameSyntax id)
            {
                continue;
            }

            if (context.Binding.InferredTypes.TryGetValue(id.Name.Text, out var type) &&
                (type.IsNil || type.MightBeNil))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "LUA2501",
                    $"Possible nil dereference: calling method on '{id.Name.Text}' which may be nil.",
                    call.Span,
                    SemanticDiagnosticSeverity.Warning,
                    $"Add 'if {id.Name.Text} then' guard before call."));
            }
        }
    }

    private static (bool AllPathsReturn, bool HasReturn) AnalyzePaths(BlockSyntax block)
    {
        var hasReturn = false;
        var allReturn = block.Statements.Count > 0;

        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case LiteralExpressionSyntax { Token.Text: "return" }:
                    hasReturn = true;
                    break;

                case IfStatementSyntax ifStmt:
                    var thenPaths = AnalyzePaths(ifStmt.ThenBlock);
                    var elsePaths = ifStmt.ElseBlock is not null
                        ? AnalyzePaths(ifStmt.ElseBlock)
                        : (AllPathsReturn: false, HasReturn: false);
                    if (!(thenPaths.AllPathsReturn && elsePaths.AllPathsReturn))
                    {
                        allReturn = false;
                    }

                    hasReturn |= thenPaths.HasReturn || elsePaths.HasReturn;
                    break;

                default:
                    allReturn = false;
                    break;
            }
        }

        return (allReturn && hasReturn, hasReturn);
    }

    private static List<(TypeInfo Type, LUAstudio.Languages.Text.TextSpan Span)> CollectReturnExpressionTypes(BlockSyntax block)
    {
        var result = new List<(TypeInfo, LUAstudio.Languages.Text.TextSpan)>();
        foreach (var stmt in block.Statements)
        {
            if (stmt is LiteralExpressionSyntax { Token.Text: "return" })
            {
                result.Add((TypeInfo.Unknown, stmt.Span));
            }
        }

        return result;
    }
}
