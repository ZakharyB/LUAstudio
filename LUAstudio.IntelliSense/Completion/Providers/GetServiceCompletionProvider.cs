using LUAstudio.IntelliSense.Roblox;

namespace LUAstudio.IntelliSense.Completion.Providers;

public sealed class GetServiceCompletionProvider : ICompletionProvider
{
    private readonly IRobloxApiDatabase _roblox;

    public GetServiceCompletionProvider(IRobloxApiDatabase roblox) => _roblox = roblox;

    public string Name => "getservice";

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var text = context.Snapshot.Content;
        var offset = context.CaretOffset;

        if (!IsInsideGetServiceString(text, offset, out var prefix))
        {
            return Array.Empty<CompletionItem>();
        }

        return _roblox.ServiceNames
            .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(s => new CompletionItem(s, s, CompletionItemKind.Class, s, priority: 200))
            .ToArray();
    }

    private static bool IsInsideGetServiceString(string text, int offset, out string prefix)
    {
        prefix = string.Empty;
        var search = text.AsSpan(0, Math.Min(offset, text.Length));
        var idx = search.LastIndexOf("GetService".AsSpan(), StringComparison.Ordinal);
        if (idx < 0)
        {
            return false;
        }

        var after = text[idx..Math.Min(offset, text.Length)];
        var quoteStart = after.IndexOfAny(['"', '\'']);
        if (quoteStart < 0)
        {
            return false;
        }

        var quote = after[quoteStart];
        var contentStart = idx + quoteStart + 1;
        if (offset < contentStart)
        {
            return false;
        }

        prefix = text[contentStart..offset];
        return true;
    }
}
