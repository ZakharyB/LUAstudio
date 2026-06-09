using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Analysis;

public interface IAnalysisOrchestrator
{
    DocumentAnalysisResult? GetLatestResult(Guid documentId);

    void RequestAnalysis(SourceSnapshot snapshot, TextSpan? changedSpan = null);

    Task<DocumentAnalysisResult> AnalyzeAsync(
        SourceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
