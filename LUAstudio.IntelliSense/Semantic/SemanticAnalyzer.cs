using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Semantic;

public sealed class SemanticAnalyzer
{
    private readonly IRobloxApiDatabase _roblox;

    public SemanticAnalyzer(IRobloxApiDatabase roblox) => _roblox = roblox;

    public SemanticModel Analyze(ParseResult parseResult)
    {
        var rootScope = new Scope();
        var diagnostics = new List<SemanticDiagnostic>();
        var inferred = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);
        var globals = new HashSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);

        VisitBlock(parseResult.Tree.Root, rootScope, declared, globals, inferred, diagnostics);

        foreach (var node in parseResult.Tree.Root.DescendantsAndSelf())
        {
            if (node is IdentifierNameSyntax id && !IsDeclaredInScopes(id.Name.Text, rootScope, declared))
            {
                if (!_roblox.GlobalTypeAliases.ContainsKey(id.Name.Text) &&
                    !_roblox.TryGetGlobal(id.Name.Text, out _) && !IsLuaBuiltin(id.Name.Text))
                {
                    diagnostics.Add(new SemanticDiagnostic(
                        "LUA2001",
                        $"Undefined global '{id.Name.Text}'.",
                        id.Span,
                        SemanticDiagnosticSeverity.Warning));
                }
            }
        }

        return new SemanticModel(parseResult, rootScope, diagnostics, inferred);
    }

    private static void VisitBlock(SyntaxNode node, Scope scope, HashSet<string> declared, HashSet<string> globals, Dictionary<string, TypeInfo> inferred, List<SemanticDiagnostic> diagnostics)
    {
        foreach (var child in node.DescendantsAndSelf())
        {
            switch (child)
            {
                case LocalStatementSyntax local:
                    Declare(local.Name.Text, SymbolKind.Local, local.Name.Span, scope, declared);
                    if (local.TypeAnnotation is not null)
                    {
                        inferred[local.Name.Text] = new TypeInfo(local.TypeAnnotation.TypeName.Text);
                    }

                    break;

                case FunctionDeclarationSyntax fn:
                    Declare(fn.Name.Text, fn.IsLocal ? SymbolKind.Function : SymbolKind.Global, fn.Name.Span, scope, declared);
                    var fnScope = new Scope(scope);
                    foreach (var p in fn.Parameters.Parameters)
                    {
                        Declare(p.Name.Text, SymbolKind.Parameter, p.Name.Span, fnScope, declared);
                    }

                    VisitBlock(fn.Body.Block, fnScope, declared, globals, inferred, diagnostics);
                    break;

                case AssignmentStatementSyntax assign when assign.Target is IdentifierNameSyntax target:
                    if (!declared.Contains(target.Name.Text))
                    {
                        globals.Add(target.Name.Text);
                    }

                    break;
            }
        }
    }

    private static void Declare(string name, SymbolKind kind, TextSpan span, Scope scope, HashSet<string> declared)
    {
        if (scope.Locals.ContainsKey(name))
        {
            return;
        }

        var symbol = new Symbol(name, kind, span);
        scope.Locals[name] = symbol;
        scope.Symbols.Add(symbol);
        declared.Add(name);
    }

    private static bool IsDeclaredInScopes(string name, Scope root, HashSet<string> declared) =>
        declared.Contains(name) || root.TryResolveLocal(name, out _);

    private static bool IsLuaBuiltin(string name) => name switch
    {
        "print" or "pairs" or "ipairs" or "next" or "type" or "tostring" or "tonumber" or "pcall" or "xpcall"
            or "error" or "assert" or "select" or "unpack" or "table" or "string" or "math" or "coroutine"
            or "require" or "setfenv" or "getfenv" or "setmetatable" or "getmetatable" or "rawget" or "rawset" => true,
        _ => false
    };
}
