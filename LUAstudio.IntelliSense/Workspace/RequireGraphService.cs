using System.Collections.Concurrent;

namespace LUAstudio.IntelliSense.Workspace;

public sealed record RequireGraphNode(string ModulePath, string? FilePath, bool IsDead);

public sealed record RequireGraphEdge(string FromModule, string ToModule, bool IsCircular);

public sealed class RequireGraphService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _edges = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _moduleFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _requireCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void RecordRequire(string fromFile, string modulePath, string? resolvedFile)
    {
        SetFileRequires(fromFile, [(modulePath, resolvedFile)]);
    }

    public void SetFileRequires(string fromFile, IReadOnlyList<(string ModulePath, string? ResolvedFile)> requires)
    {
        var fromKey = Normalize(fromFile);

        lock (_lock)
        {
            if (_edges.TryRemove(fromKey, out var previousTargets))
            {
                foreach (var oldTarget in previousTargets)
                {
                    DecrementRequireCount(oldTarget);
                }
            }

            if (requires.Count == 0)
            {
                return;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (modulePath, resolvedFile) in requires)
            {
                var toKey = Normalize(modulePath);
                set.Add(toKey);
                _moduleFiles[toKey] = resolvedFile;
                IncrementRequireCount(toKey);

                if (!string.IsNullOrWhiteSpace(resolvedFile))
                {
                    _moduleFiles[Normalize(resolvedFile)] = resolvedFile;
                }
            }

            _edges[fromKey] = set;
            _moduleFiles[fromKey] = fromFile;
        }
    }

    public void RebuildDeadModules(IEnumerable<string> allModuleKeys)
    {
        lock (_lock)
        {
            foreach (var key in allModuleKeys)
            {
                _moduleFiles.TryAdd(key, null);
            }
        }
    }

    public bool HasCircularDependency(string fromFile, string toModule)
    {
        lock (_lock)
        {
            var from = Normalize(fromFile);
            var to = Normalize(toModule);
            return HasCycle(from, to, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<RequireGraphEdge> GetEdges()
    {
        lock (_lock)
        {
            var result = new List<RequireGraphEdge>();
            foreach (var (from, targets) in _edges)
            {
                foreach (var to in targets)
                {
                    result.Add(new RequireGraphEdge(
                        FormatLabel(from),
                        FormatLabel(to),
                        HasCycle(from, to, new HashSet<string>(StringComparer.OrdinalIgnoreCase))));
                }
            }

            return result;
        }
    }

    public IReadOnlyList<RequireGraphNode> GetNodes()
    {
        lock (_lock)
        {
            var referenced = new HashSet<string>(_requireCounts.Keys, StringComparer.OrdinalIgnoreCase);
            return _moduleFiles.Select(kvp => new RequireGraphNode(
                FormatLabel(kvp.Key),
                kvp.Value,
                !referenced.Contains(kvp.Key) && !IsEntryPoint(kvp.Key))).ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _edges.Clear();
            _moduleFiles.Clear();
            _requireCounts.Clear();
        }
    }

    private void IncrementRequireCount(string key)
    {
        _requireCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    private void DecrementRequireCount(string key)
    {
        if (!_requireCounts.TryGetValue(key, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _requireCounts.TryRemove(key, out _);
        }
        else
        {
            _requireCounts[key] = count - 1;
        }
    }

    private bool HasCycle(string start, string target, HashSet<string> visiting)
    {
        if (!visiting.Add(target))
        {
            return target == start;
        }

        if (!_edges.TryGetValue(target, out var next))
        {
            visiting.Remove(target);
            return false;
        }

        foreach (var n in next)
        {
            if (HasCycle(start, n, visiting))
            {
                return true;
            }
        }

        visiting.Remove(target);
        return false;
    }

    private static bool IsEntryPoint(string module) =>
        module.Equals("main", StringComparison.OrdinalIgnoreCase) ||
        module.EndsWith(".init", StringComparison.OrdinalIgnoreCase) ||
        module.EndsWith("init.lua", StringComparison.OrdinalIgnoreCase) ||
        module.EndsWith("init.luau", StringComparison.OrdinalIgnoreCase);

    private static string FormatLabel(string path)
    {
        var normalized = Normalize(path);
        if (normalized.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalized);
        }

        return normalized;
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim().Trim('"', '\'');
}
