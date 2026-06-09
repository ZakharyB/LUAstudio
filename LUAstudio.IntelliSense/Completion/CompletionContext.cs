using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Completion;

public enum CompletionTriggerKind
{
    Default,
    Dot,
    Colon,
    String,
    Invoke
}

public sealed class CompletionContext
{
    public CompletionContext(
        SourceSnapshot snapshot,
        int caretOffset,
        SemanticModel? semanticModel,
        string triggerPrefix,
        CompletionTriggerKind triggerKind = CompletionTriggerKind.Default)
    {
        Snapshot = snapshot;
        CaretOffset = caretOffset;
        SemanticModel = semanticModel;
        TriggerPrefix = triggerPrefix;
        TriggerKind = triggerKind;
    }

    public SourceSnapshot Snapshot { get; }

    public int CaretOffset { get; }

    public SemanticModel? SemanticModel { get; }

    public string TriggerPrefix { get; }

    public CompletionTriggerKind TriggerKind { get; }

    public SyntaxNode? NodeAtCaret =>
        SemanticModel?.Tree.Root.FindNodeAt(Math.Max(0, CaretOffset - 1));

    public char? TriggerCharacter
    {
        get
        {
            if (CaretOffset <= 0 || CaretOffset > Snapshot.Content.Length)
            {
                return null;
            }

            return Snapshot.Content[CaretOffset - 1];
        }
    }
}
