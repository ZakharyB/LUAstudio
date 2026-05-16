namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class KeywordSnippetCompletionProvider : ICompletionProvider
{
    private static readonly (string Keyword, string Insert, CompletionItemKind Kind, int Priority)[] Entries =
    [
        ("function", "function ${1:name}(${2:args})\n\t${0}\nend", CompletionItemKind.Snippet, 120),
        ("local function", "local function ${1:name}(${2:args})\n\t${0}\nend", CompletionItemKind.Snippet, 115),
        ("local", "local ${1:name} = ${0}", CompletionItemKind.Snippet, 110),
        ("if", "if ${1:cond} then\n\t${0}\nend", CompletionItemKind.Snippet, 100),
        ("for", "for ${1:i}, ${2:v} in ipairs(${3:table}) do\n\t${0}\nend", CompletionItemKind.Snippet, 100),
        ("while", "while ${1:cond} do\n\t${0}\nend", CompletionItemKind.Snippet, 95),
        ("repeat", "repeat\n\t${0}\nuntil ${1:cond}", CompletionItemKind.Snippet, 90),
        ("do", "do\n\t${0}\nend", CompletionItemKind.Snippet, 85),
        ("return", "return ${0}", CompletionItemKind.Keyword, 80),
        ("and", "and", CompletionItemKind.Keyword, 50),
        ("break", "break", CompletionItemKind.Keyword, 50),
        ("do", "do", CompletionItemKind.Keyword, 50),
        ("else", "else", CompletionItemKind.Keyword, 50),
        ("elseif", "elseif", CompletionItemKind.Keyword, 50),
        ("end", "end", CompletionItemKind.Keyword, 50),
        ("false", "false", CompletionItemKind.Keyword, 50),
        ("for", "for", CompletionItemKind.Keyword, 50),
        ("goto", "goto", CompletionItemKind.Keyword, 50),
        ("if", "if", CompletionItemKind.Keyword, 50),
        ("in", "in", CompletionItemKind.Keyword, 50),
        ("local", "local", CompletionItemKind.Keyword, 50),
        ("nil", "nil", CompletionItemKind.Keyword, 50),
        ("not", "not", CompletionItemKind.Keyword, 50),
        ("or", "or", CompletionItemKind.Keyword, 50),
        ("repeat", "repeat", CompletionItemKind.Keyword, 50),
        ("then", "then", CompletionItemKind.Keyword, 50),
        ("true", "true", CompletionItemKind.Keyword, 50),
        ("until", "until", CompletionItemKind.Keyword, 50),
        ("while", "while", CompletionItemKind.Keyword, 50),
        ("type", "type ${1:Name} = ${0}", CompletionItemKind.Snippet, 105),
        ("export", "export", CompletionItemKind.Keyword, 55),
        ("continue", "continue", CompletionItemKind.Keyword, 55),
        ("typeof", "typeof(${0})", CompletionItemKind.Snippet, 75),
    ];

    public string Name => "keywords";

    public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var prefix = context.TriggerPrefix;
        var items = new List<CompletionItem>();

        foreach (var (keyword, insert, kind, priority) in Entries)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                !keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new CompletionItem(keyword, insert, kind, priority: priority));
        }

        return Task.FromResult<IReadOnlyList<CompletionItem>>(items);
    }
}
