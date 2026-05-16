using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Completion;

public sealed class CompletionContext
{
    public CompletionContext(
        SourceSnapshot snapshot,
        int caretOffset,
        SemanticModel? semanticModel,
        string triggerPrefix)
    {
        Snapshot = snapshot;
        CaretOffset = caretOffset;
        SemanticModel = semanticModel;
        TriggerPrefix = triggerPrefix;
    }

    public SourceSnapshot Snapshot { get; }

    public int CaretOffset { get; }

    public SemanticModel? SemanticModel { get; }

    public string TriggerPrefix { get; }

    public SyntaxNode? NodeAtCaret =>
        SemanticModel?.Tree.Root.FindNodeAt(Math.Max(0, CaretOffset - 1));
}
