using System.Diagnostics.CodeAnalysis;
using System.Text;
using Luau;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class InMemoryLuauRequirer : LuauRequirer
{
    private readonly ModuleResolver _modules;

    public InMemoryLuauRequirer(ModuleResolver modules) => _modules = modules;

    protected override bool TryLoadModule(LuauState state, string fullPath, string requireArgument)
    {
        if (!_modules.TryGetSource(fullPath, out var source) &&
            !_modules.TryGetSource(requireArgument, out source))
        {
            return false;
        }

        var chunkName = Path.GetFileNameWithoutExtension(fullPath);
        Span<byte> chunkNameBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(chunkName.Length)];
        var chunkNameLength = Encoding.UTF8.GetBytes(chunkName, chunkNameBytes);

        var results = LuauScriptRunner.DoString(state, Encoding.UTF8.GetBytes(source), chunkNameBytes[..chunkNameLength]);
        if (results.Length != 1)
        {
            throw new LuauException($"Module '{requireArgument}' must return exactly one value.");
        }

        state.Push(results[0]);
        return true;
    }

    protected override bool TryGetAliasPath(string alias, [NotNullWhen(true)] out string? path)
    {
        path = null;
        return false;
    }

    protected override string GetCacheKey(string path) =>
        path.Replace('\\', '/').Trim();
}
