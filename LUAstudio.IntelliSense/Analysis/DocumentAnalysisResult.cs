using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Parsing;

namespace LUAstudio.IntelliSense.Analysis;

public sealed class DocumentAnalysisResult
{
    public DocumentAnalysisResult(
        ParseResult parseResult,
        SemanticModel semanticModel,
        TimeSpan elapsed)
    {
        ParseResult = parseResult;
        SemanticModel = semanticModel;
        Elapsed = elapsed;
    }

    public ParseResult ParseResult { get; }

    public SemanticModel SemanticModel { get; }

    public TimeSpan Elapsed { get; }
}
