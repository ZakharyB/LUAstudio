using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Semantic;

public sealed record SymbolUsage(Symbol Symbol, int ReferenceCount);

public sealed record RequireEdge(string ModulePath, TextSpan Span, string? ResolvedFilePath);

public sealed record BoundFunction(
    FunctionDeclarationSyntax Node,
    Scope Scope,
    TypeInfo? DeclaredReturnType,
    IReadOnlyList<TypeInfo> ReturnTypes);

public sealed class SemanticBindingResult
{
    public required Scope RootScope { get; init; }

    public required Dictionary<string, TypeInfo> InferredTypes { get; init; }

    public required Dictionary<Symbol, SymbolUsage> SymbolUsages { get; init; }

    public required List<RequireEdge> RequireEdges { get; init; }

    public required List<BoundFunction> Functions { get; init; }

    public required List<Scope> AllScopes { get; init; }

    public required HashSet<string> AssignedGlobals { get; init; }

    public required HashSet<string> ReadGlobals { get; init; }
}

public sealed class SemanticBinder
{
    private Scope _currentScope = null!;
    private readonly Dictionary<string, TypeInfo> _inferred = new(StringComparer.Ordinal);
    private readonly Dictionary<Symbol, SymbolUsage> _usages = new();
    private readonly List<RequireEdge> _requires = [];
    private readonly List<BoundFunction> _functions = [];
    private readonly List<Scope> _allScopes = [];
    private readonly HashSet<string> _assignedGlobals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readGlobals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _declaredGlobals = new(StringComparer.Ordinal);
    private string? _filePath;

    public SemanticBindingResult Bind(ParseResult parseResult)
    {
        _currentScope = new Scope();
        _allScopes.Add(_currentScope);
        _inferred.Clear();
        _usages.Clear();
        _requires.Clear();
        _functions.Clear();
        _allScopes.Clear();
        _assignedGlobals.Clear();
        _readGlobals.Clear();
        _declaredGlobals.Clear();
        _filePath = parseResult.Snapshot.FilePath;

        if (parseResult.Tree.Root is CompilationUnitSyntax unit)
        {
            BindStatements(unit.Statements, _currentScope);
            CollectRequireCalls(unit);
        }

        return new SemanticBindingResult
        {
            RootScope = _currentScope,
            InferredTypes = _inferred,
            SymbolUsages = _usages,
            RequireEdges = _requires,
            Functions = _functions,
            AllScopes = _allScopes,
            AssignedGlobals = _assignedGlobals,
            ReadGlobals = _readGlobals
        };
    }

    private void BindStatements(IReadOnlyList<SyntaxNode> statements, Scope scope)
    {
        var previous = _currentScope;
        _currentScope = scope;

        foreach (var stmt in statements)
        {
            BindStatement(stmt);
        }

        _currentScope = previous;
    }

    private void BindStatement(SyntaxNode stmt)
    {
        switch (stmt)
        {
            case LocalStatementSyntax local:
                BindLocal(local);
                break;

            case FunctionDeclarationSyntax fn:
                BindFunction(fn);
                break;

            case AssignmentStatementSyntax assign:
                BindAssignment(assign);
                break;

            case IfStatementSyntax ifStmt:
                BindExpression(ifStmt.Condition);
                BindBlock(ifStmt.ThenBlock);
                if (ifStmt.ElseBlock is not null)
                {
                    BindBlock(ifStmt.ElseBlock);
                }

                break;

            case WhileStatementSyntax whileStmt:
                BindExpression(whileStmt.Condition);
                BindBlock(whileStmt.Body);
                break;

            case ForStatementSyntax forStmt:
                BindBlock(forStmt.Body);
                break;

            case BlockSyntax block:
                BindBlock(block);
                break;

            default:
                BindExpression(stmt);
                break;
        }
    }

    private void BindLocal(LocalStatementSyntax local)
    {
        CheckShadow(local.Name.Text, local.Name.Span);
        var type = local.TypeAnnotation is not null
            ? TypeInfo.FromAnnotation(local.TypeAnnotation.TypeName.Text)
            : InferType(local.Initializer);

        var symbol = Declare(local.Name.Text, SymbolKind.Local, local.Name.Span, type);
        if (local.Initializer is not null)
        {
            BindExpression(local.Initializer);
        }
    }

    private void BindFunction(FunctionDeclarationSyntax fn)
    {
        CheckShadow(fn.Name.Text, fn.Name.Span);
        var returnType = fn.ReturnType is not null
            ? TypeInfo.FromAnnotation(fn.ReturnType.TypeName.Text)
            : null;

        var kind = fn.IsLocal ? SymbolKind.Function : SymbolKind.Global;
        var symbol = Declare(fn.Name.Text, kind, fn.Name.Span, returnType);

        if (!fn.IsLocal)
        {
            _declaredGlobals.Add(fn.Name.Text);
        }

        var fnScope = new Scope(_currentScope);
        _allScopes.Add(fnScope);
        var previous = _currentScope;
        _currentScope = fnScope;

        foreach (var p in fn.Parameters.Parameters)
        {
            CheckShadow(p.Name.Text, p.Name.Span);
            var paramType = p.TypeAnnotation is not null
                ? TypeInfo.FromAnnotation(p.TypeAnnotation.TypeName.Text)
                : TypeInfo.Unknown;
            Declare(p.Name.Text, SymbolKind.Parameter, p.Name.Span, paramType);
        }

        BindBlock(fn.Body.Block);

        _functions.Add(new BoundFunction(fn, fnScope, returnType, CollectReturnTypes(fn.Body.Block)));

        _currentScope = previous;
        TrackUsage(symbol);
    }

    private void BindAssignment(AssignmentStatementSyntax assign)
    {
        BindExpression(assign.Value);

        if (assign.Target is IdentifierNameSyntax id)
        {
            if (!_currentScope.TryResolveLocal(id.Name.Text, out _))
            {
                _assignedGlobals.Add(id.Name.Text);
            }

            var valueType = InferType(assign.Value);
            if (_currentScope.TryResolveLocal(id.Name.Text, out var sym) && sym is not null)
            {
                _inferred[id.Name.Text] = MergeTypes(
                    _inferred.GetValueOrDefault(id.Name.Text, TypeInfo.Unknown),
                    valueType);
            }
        }
        else
        {
            BindExpression(assign.Target);
        }
    }

    private void BindBlock(BlockSyntax block) => BindStatements(block.Statements, _currentScope);

    private void BindExpression(SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        switch (node)
        {
            case IdentifierNameSyntax id:
                ResolveReference(id);
                break;

            case MemberAccessExpressionSyntax member:
                BindExpression(member.Expression);
                break;

            case CallExpressionSyntax call:
                if (TryExtractRequireModule(call, out var requirePath, out var requireSpan))
                {
                    _requires.Add(new RequireEdge(requirePath, requireSpan, null));
                }

                BindExpression(call.Target);
                foreach (var arg in call.Arguments)
                {
                    BindExpression(arg);
                }

                break;

            case RequireCallSyntax req:
                _requires.Add(new RequireEdge(
                    TrimQuotes(req.ModulePath.Text),
                    req.Span,
                    null));
                break;

            case TableExpressionSyntax table:
                foreach (var field in table.Fields)
                {
                    if (field.Key is not null)
                    {
                        BindExpression(field.Key);
                    }

                    BindExpression(field.Value);
                }

                break;

            case LiteralExpressionSyntax:
                break;

            default:
                foreach (var child in node.Children)
                {
                    BindExpression(child);
                }

                break;
        }
    }

    private void ResolveReference(IdentifierNameSyntax id)
    {
        if (_currentScope.TryResolveLocal(id.Name.Text, out var sym) && sym is not null)
        {
            TrackUsage(sym);
            return;
        }

        _readGlobals.Add(id.Name.Text);
    }

    private Symbol Declare(string name, SymbolKind kind, TextSpan span, TypeInfo? type)
    {
        var symbol = new Symbol(name, kind, span, _filePath, typeName: type?.DisplayName);
        _currentScope.Locals[name] = symbol;
        _currentScope.Symbols.Add(symbol);
        _usages[symbol] = new SymbolUsage(symbol, 0);

        if (type is not null && kind is SymbolKind.Local or SymbolKind.Parameter)
        {
            _inferred[name] = type;
        }

        return symbol;
    }

    private void CheckShadow(string name, TextSpan span)
    {
        if (_currentScope.TryResolveLocal(name, out _))
        {
            // Shadowing is reported by analyzer using scope chain.
        }
    }

    private void TrackUsage(Symbol symbol)
    {
        if (_usages.TryGetValue(symbol, out var usage))
        {
            _usages[symbol] = usage with { ReferenceCount = usage.ReferenceCount + 1 };
        }
    }

    private static TypeInfo InferType(SyntaxNode? node) => node switch
    {
        null => TypeInfo.Unknown,
        LiteralExpressionSyntax lit => TypeInfo.FromLiteralToken(lit.Token.Text),
        TableExpressionSyntax table => InferTableShape(table),
        IdentifierNameSyntax id when id.Name.Text == "nil" => TypeInfo.Nil,
        FunctionDeclarationSyntax => TypeInfo.Function,
        _ => TypeInfo.Unknown
    };

    private static TypeInfo InferTableShape(TableExpressionSyntax table)
    {
        var shape = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);
        foreach (var field in table.Fields)
        {
            var key = field.Key switch
            {
                IdentifierNameSyntax id => id.Name.Text,
                LiteralExpressionSyntax lit => TrimQuotes(lit.Token.Text),
                _ => null
            };

            if (key is null)
            {
                continue;
            }

            shape[key] = InferType(field.Value);
        }

        return new TypeInfo
        {
            DisplayName = "table",
            TableShape = shape
        };
    }

    private static IReadOnlyList<TypeInfo> CollectReturnTypes(BlockSyntax block)
    {
        var types = new List<TypeInfo>();
        foreach (var stmt in block.Statements)
        {
            if (stmt is LiteralExpressionSyntax { Token.Text: "return" })
            {
                types.Add(TypeInfo.Unknown);
            }
        }

        return types;
    }

    private static TypeInfo MergeTypes(TypeInfo existing, TypeInfo incoming)
    {
        if (existing.IsUnknown)
        {
            return incoming;
        }

        if (existing.DisplayName == incoming.DisplayName)
        {
            return existing;
        }

        return TypeInfo.Union(existing, incoming);
    }

    private static string? MergeTypeName(string? existing, TypeInfo incoming)
    {
        if (string.IsNullOrEmpty(existing) || existing == "unknown")
        {
            return incoming.DisplayName;
        }

        if (existing == incoming.DisplayName)
        {
            return existing;
        }

        return TypeInfo.Union(TypeInfo.FromAnnotation(existing), incoming).DisplayName;
    }

    private static string TrimQuotes(string text) => text.Trim().Trim('"', '\'');

    private void CollectRequireCalls(SyntaxNode node)
    {
        switch (node)
        {
            case RequireCallSyntax req:
                AddRequireEdge(TrimQuotes(req.ModulePath.Text), req.Span);
                break;

            case CallExpressionSyntax call when TryExtractRequireModule(call, out var path, out var span):
                AddRequireEdge(path, span);
                break;
        }

        foreach (var child in node.Children)
        {
            CollectRequireCalls(child);
        }
    }

    private void AddRequireEdge(string modulePath, TextSpan span)
    {
        if (_requires.Any(r =>
                r.Span.Start == span.Start &&
                r.Span.End == span.End &&
                string.Equals(r.ModulePath, modulePath, StringComparison.Ordinal)))
        {
            return;
        }

        _requires.Add(new RequireEdge(modulePath, span, null));
    }

    private static bool TryExtractRequireModule(
        CallExpressionSyntax call,
        out string modulePath,
        out TextSpan span)
    {
        modulePath = string.Empty;
        span = call.Span;

        if (call.Target is not IdentifierNameSyntax { Name.Text: "require" })
        {
            return false;
        }

        if (call.Arguments.Count == 0 ||
            call.Arguments[0] is not LiteralExpressionSyntax lit)
        {
            return false;
        }

        modulePath = TrimQuotes(lit.Token.Text);
        return modulePath.Length > 0;
    }
}
