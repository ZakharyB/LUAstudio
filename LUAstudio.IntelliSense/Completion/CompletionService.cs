namespace LUAstudio.IntelliSense.Completion;

public interface ICompletionService
{
    Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class CompletionService : ICompletionService
{
    private readonly IReadOnlyList<ICompletionProvider> _providers;
    private readonly CompletionResultCache _cache = new();

    public CompletionService(IEnumerable<ICompletionProvider> providers) =>
        _providers = providers.ToArray();

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGet(
                context.Snapshot.DocumentId,
                context.Snapshot.Version,
                context.CaretOffset,
                context.TriggerPrefix,
                out var cached) && cached is not null)
        {
            return cached;
        }

        var tasks = _providers.Select(p => p.GetCompletionsAsync(context, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var merged = new Dictionary<string, CompletionItem>(StringComparer.Ordinal);
        foreach (var batch in results)
        {
            foreach (var item in batch)
            {
                if (!merged.TryGetValue(item.DisplayText, out var existing) || item.Priority > existing.Priority)
                {
                    item.Score = ScoreItem(item, context.TriggerPrefix);
                    merged[item.DisplayText] = item;
                }
            }
        }

        var result = merged.Values
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.Priority)
            .ThenBy(i => i.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cache.Store(
            context.Snapshot.DocumentId,
            context.Snapshot.Version,
            context.CaretOffset,
            context.TriggerPrefix,
            result);

        return result;
    }

    private static double ScoreItem(CompletionItem item, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return item.Priority;
        }

        if (item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return item.Priority + 200 - prefix.Length;
        }

        return FuzzyMatch(prefix, item.DisplayText) ? item.Priority + 50 : 0;
    }

    private static bool FuzzyMatch(string prefix, string candidate)
    {
        var pi = 0;
        for (var ci = 0; ci < candidate.Length && pi < prefix.Length; ci++)
        {
            if (char.ToLowerInvariant(candidate[ci]) == char.ToLowerInvariant(prefix[pi]))
            {
                pi++;
            }
        }

        return pi == prefix.Length;
    }
}
