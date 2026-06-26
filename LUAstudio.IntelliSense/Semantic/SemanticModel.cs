using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax;

namespace LUAstudio.IntelliSense.Semantic;

public sealed class SemanticModel
{
    public SemanticModel(
        ParseResult parseResult,
        Scope rootScope,
        IReadOnlyList<SemanticDiagnostic> diagnostics,
        IReadOnlyDictionary<string, TypeInfo> inferredTypes,
        SemanticBindingResult? binding = null)
    {
        ParseResult = parseResult;
        RootScope = rootScope;
        Diagnostics = diagnostics;
        InferredTypes = inferredTypes;
        Binding = binding;
    }

    public ParseResult ParseResult { get; }

    public SyntaxTree Tree => ParseResult.Tree;

    public Scope RootScope { get; }

    public IReadOnlyList<SemanticDiagnostic> Diagnostics { get; }

    public IReadOnlyDictionary<string, TypeInfo> InferredTypes { get; }

    public SemanticBindingResult? Binding { get; }
}

public sealed record SemanticDiagnostic(
    string Code,
    string Message,
    LUAstudio.Languages.Text.TextSpan Span,
    SemanticDiagnosticSeverity Severity,
    string? FixSuggestion = null);

public enum SemanticDiagnosticSeverity
{
    Hint,
    Info,
    Warning,
    Error
}
