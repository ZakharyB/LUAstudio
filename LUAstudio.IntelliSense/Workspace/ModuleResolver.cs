using System.Collections.Concurrent;
using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense.Workspace;

public sealed class ModuleResolver : IModuleResolver
{
    private readonly ConcurrentDictionary<string, Symbol> _modules = new(StringComparer.OrdinalIgnoreCase);
    private string[] _roots = [];

    public Symbol? ResolveModule(string modulePath, string? fromFilePath)
    {
        var key = NormalizeModuleKey(modulePath);
        if (_modules.TryGetValue(key, out var symbol))
        {
            return symbol;
        }

        foreach (var root in _roots)
        {
            var candidate = Path.Combine(root, key.Replace('.', Path.DirectorySeparatorChar) + ".lua");
            if (File.Exists(candidate))
            {
                var mod = new Symbol(Path.GetFileNameWithoutExtension(candidate), SymbolKind.Module, default, candidate);
                _modules[key] = mod;
                return mod;
            }

            var luau = Path.ChangeExtension(candidate, ".luau");
            if (File.Exists(luau))
            {
                var mod = new Symbol(Path.GetFileNameWithoutExtension(luau), SymbolKind.Module, default, luau);
                _modules[key] = mod;
                return mod;
            }
        }

        return null;
    }

    public void RebuildIndex(IEnumerable<string> workspaceRootPaths)
    {
        _roots = workspaceRootPaths.ToArray();
        _modules.Clear();

        foreach (var root in _roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.lua", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(root, "*.luau", SearchOption.AllDirectories)))
            {
                var relative = Path.GetRelativePath(root, file);
                var key = NormalizeModuleKey(Path.ChangeExtension(relative, null).Replace(Path.DirectorySeparatorChar, '.'));
                _modules[key] = new Symbol(Path.GetFileNameWithoutExtension(file), SymbolKind.Module, default, file);
            }
        }
    }

    private static string NormalizeModuleKey(string modulePath) =>
        modulePath.Trim().Trim('"', '\'').Replace('/', '.').Replace('\\', '.');
}
