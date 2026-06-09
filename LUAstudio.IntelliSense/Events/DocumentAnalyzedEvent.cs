using LUAstudio.IntelliSense.Analysis;

namespace LUAstudio.IntelliSense.Events;

public sealed class DocumentAnalyzedEvent
{
    public DocumentAnalyzedEvent(Guid documentId, int version, DocumentAnalysisResult result)
    {
        DocumentId = documentId;
        Version = version;
        Result = result;
    }

    public Guid DocumentId { get; }

    public int Version { get; }

    public DocumentAnalysisResult Result { get; }
}
