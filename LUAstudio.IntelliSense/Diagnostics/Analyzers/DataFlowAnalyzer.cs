using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class DataFlowAnalyzer : IDiagnosticAnalyzer
{
    public int Order => 30;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var fn in context.Binding.Functions)
        {
            AnalyzeFunction(fn, diagnostics);
        }
    }

    private static void AnalyzeFunction(BoundFunction fn, ICollection<SemanticDiagnostic> diagnostics)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var nilPropagated = new HashSet<string>(StringComparer.Ordinal);

        foreach (var param in fn.Node.Parameters.Parameters)
        {
            declared.Add(param.Name.Text);
        }

        WalkBlock(fn.Node.Body.Block, fn.Scope, assigned, declared, nilPropagated, diagnostics);
    }

    private static void WalkBlock(
        BlockSyntax block,
        Scope scope,
        HashSet<string> assigned,
        HashSet<string> declared,
        HashSet<string> nilPropagated,
        ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case LocalStatementSyntax local:
                    declared.Add(local.Name.Text);
                    if (local.Initializer is LiteralExpressionSyntax { Token.Text: "nil" })
                    {
                        nilPropagated.Add(local.Name.Text);
                    }
                    else if (local.Initializer is IdentifierNameSyntax idInit &&
                             nilPropagated.Contains(idInit.Name.Text))
                    {
                        nilPropagated.Add(local.Name.Text);
                        diagnostics.Add(new SemanticDiagnostic(
                            "LUA2203",
                            $"Nil may propagate to '{local.Name.Text}' from '{idInit.Name.Text}'.",
                            local.Name.Span,
                            SemanticDiagnosticSeverity.Info,
                            "Add a nil guard before use."));
                    }
                    else if (local.Initializer is null)
                    {
                        diagnostics.Add(new SemanticDiagnostic(
                            "LUA2201",
                            $"Local '{local.Name.Text}' is declared but never assigned an initial value.",
                            local.Name.Span,
                            SemanticDiagnosticSeverity.Hint,
                            $"Initialize '{local.Name.Text}' or assign before use."));
                    }

                    CheckUseBeforeAssign(local.Initializer, assigned, declared, diagnostics);
                    break;

                case AssignmentStatementSyntax assign when assign.Target is IdentifierNameSyntax target:
                    if (!declared.Contains(target.Name.Text) && !assigned.Contains(target.Name.Text) &&
                        scope.TryResolveLocal(target.Name.Text, out _))
                    {
                        diagnostics.Add(new SemanticDiagnostic(
                            "LUA2202",
                            $"Variable '{target.Name.Text}' may be used before assignment.",
                            target.Span,
                            SemanticDiagnosticSeverity.Warning,
                            $"Assign to '{target.Name.Text}' before reading it."));
                    }

                    assigned.Add(target.Name.Text);
                    if (assign.Value is LiteralExpressionSyntax { Token.Text: "nil" })
                    {
                        nilPropagated.Add(target.Name.Text);
                    }

                    CheckUseBeforeAssign(assign.Value, assigned, declared, diagnostics);
                    break;

                case IfStatementSyntax ifStmt:
                    CheckUseBeforeAssign(ifStmt.Condition, assigned, declared, diagnostics);
                    WalkBlock(ifStmt.ThenBlock, scope, assigned, declared, nilPropagated, diagnostics);
                    if (ifStmt.ElseBlock is not null)
                    {
                        WalkBlock(ifStmt.ElseBlock, scope, assigned, declared, nilPropagated, diagnostics);
                    }

                    break;

                case WhileStatementSyntax whileStmt:
                    CheckUseBeforeAssign(whileStmt.Condition, assigned, declared, diagnostics);
                    WalkBlock(whileStmt.Body, scope, assigned, declared, nilPropagated, diagnostics);
                    break;

                case ForStatementSyntax forStmt:
                    WalkBlock(forStmt.Body, scope, assigned, declared, nilPropagated, diagnostics);
                    break;

                default:
                    CheckUseBeforeAssign(stmt, assigned, declared, diagnostics);
                    break;
            }
        }
    }

    private static void CheckUseBeforeAssign(
        SyntaxNode? node,
        HashSet<string> assigned,
        HashSet<string> declared,
        ICollection<SemanticDiagnostic> diagnostics)
    {
        if (node is null)
        {
            return;
        }

        foreach (var id in node.DescendantsAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!declared.Contains(id.Name.Text) || assigned.Contains(id.Name.Text))
            {
                continue;
            }

            if (!IsReferenceSite(id))
            {
                continue;
            }

            diagnostics.Add(new SemanticDiagnostic(
                "LUA2202",
                $"Variable '{id.Name.Text}' may be used before assignment.",
                id.Span,
                SemanticDiagnosticSeverity.Warning,
                $"Assign '{id.Name.Text}' before use."));
        }
    }

    private static bool IsReferenceSite(IdentifierNameSyntax id) =>
        id.Parent is not LocalStatementSyntax &&
        id.Parent is not ParameterSyntax &&
        (id.Parent is not FunctionDeclarationSyntax fn || fn.Name != id.Name);
}
