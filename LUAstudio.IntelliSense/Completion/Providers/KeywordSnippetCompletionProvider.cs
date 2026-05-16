namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class KeywordSnippetCompletionProvider : ICompletionProvider
{
    public string Name => "keywords";

    public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(context.TriggerPrefix) && context.TriggerPrefix.Length > 0)
        {
            return Task.FromResult<IReadOnlyList<CompletionItem>>([]);
        }

        IReadOnlyList<CompletionItem> items =
        [
            new("function", "function ${1:name}(${2:args})\n\t${0}\nend", CompletionItemKind.Snippet, priority: 50),
            new("local function", "local function ${1:name}(${2:args})\n\t${0}\nend", CompletionItemKind.Snippet, priority: 50),
            new("if", "if ${1:cond} then\n\t${0}\nend", CompletionItemKind.Snippet, priority: 40),
            new("for", "for ${1:i}, ${2:v} in ipairs(${3:table}) do\n\t${0}\nend", CompletionItemKind.Snippet, priority: 40),
            new("local", "local ${1:name} = ${0}", CompletionItemKind.Snippet, priority: 45),
        ];

        return Task.FromResult(items);
    }
}
