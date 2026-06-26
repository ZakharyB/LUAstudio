using System.Collections.Concurrent;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class ModuleResolver
{
    private readonly ConcurrentDictionary<string, string> _modules = new(StringComparer.OrdinalIgnoreCase);

    public void SetModules(IReadOnlyList<(string Path, string Source)> modules)
    {
        _modules.Clear();
        foreach (var (path, source) in modules)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            _modules[Normalize(path)] = source;
        }
    }

    public void SetModule(string path, string source) =>
        _modules[Normalize(path)] = source;

    public bool TryGetSource(string path, out string source) =>
        _modules.TryGetValue(Normalize(path), out source!);

    public IReadOnlyDictionary<string, string> Snapshot() =>
        new Dictionary<string, string>(_modules, StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim();
}
