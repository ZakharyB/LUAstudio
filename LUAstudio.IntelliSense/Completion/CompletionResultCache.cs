using System.Collections.Concurrent;

namespace LUAstudio.IntelliSense.Completion;

public sealed class CompletionResultCache
{
    private readonly ConcurrentDictionary<string, (int Version, IReadOnlyList<CompletionItem> Items)> _cache = new();

    public bool TryGet(Guid documentId, int version, int caretOffset, string prefix, out IReadOnlyList<CompletionItem>? items)
    {
        var key = $"{documentId}:{version}:{caretOffset}:{prefix}";
        if (_cache.TryGetValue(key, out var entry) && entry.Version == version)
        {
            items = entry.Items;
            return true;
        }

        items = null;
        return false;
    }

    public void Store(Guid documentId, int version, int caretOffset, string prefix, IReadOnlyList<CompletionItem> items)
    {
        var key = $"{documentId}:{version}:{caretOffset}:{prefix}";
        _cache[key] = (version, items);
    }

    public void Invalidate(Guid documentId)
    {
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(documentId.ToString(), StringComparison.Ordinal)))
        {
            _cache.TryRemove(key, out _);
        }
    }
}
