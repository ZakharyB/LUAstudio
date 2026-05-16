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
        IReadOnlyDictionary<string, TypeInfo> inferredTypes)
    {
        ParseResult = parseResult;
        RootScope = rootScope;
        Diagnostics = diagnostics;
        InferredTypes = inferredTypes;
    }

    public ParseResult ParseResult { get; }

    public SyntaxTree Tree => ParseResult.Tree;

    public Scope RootScope { get; }

    public IReadOnlyList<SemanticDiagnostic> Diagnostics { get; }

    public IReadOnlyDictionary<string, TypeInfo> InferredTypes { get; }
}

public sealed record SemanticDiagnostic(
    string Code,
    string Message,
    LUAstudio.Languages.Text.TextSpan Span,
    SemanticDiagnosticSeverity Severity);

public enum SemanticDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record TypeInfo(string DisplayName, bool IsNullable = false);
