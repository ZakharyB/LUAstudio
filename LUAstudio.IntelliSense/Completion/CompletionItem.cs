namespace LUAstudio.IntelliSense.Completion;

public enum CompletionItemKind
{
    Text,
    Method,
    Function,
    Field,
    Variable,
    Class,
    Module,
    Property,
    Keyword,
    Snippet,
    Service
}

public sealed class CompletionItem
{
    public CompletionItem(
        string displayText,
        string insertText,
        CompletionItemKind kind,
        string? detail = null,
        string? documentation = null,
        int priority = 0)
    {
        DisplayText = displayText;
        InsertText = insertText;
        Kind = kind;
        Detail = detail;
        Documentation = documentation;
        Priority = priority;
    }

    public string DisplayText { get; }

    public string InsertText { get; }

    public CompletionItemKind Kind { get; }

    public string? Detail { get; }

    public string? Documentation { get; }

    public int Priority { get; }

    public double Score { get; set; }
}
