using System.Text;
using Luau;

namespace LUAstudio.ExecutionHost.Runtime;

internal static class LuauScriptRunner
{
    private const int MultRet = -1;

    public static LuauValue[] DoString(LuauState state, ReadOnlySpan<byte> source, ReadOnlySpan<byte> chunkName = default)
    {
        var bytecode = LuauBytecodeCompiler.Compile(source);
        state.SetTop(0);
        state.Load(bytecode, chunkName);
        state.Call(0, MultRet);

        var count = state.GetTop();
        var results = new LuauValue[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = state.ToValue(index + 1);
        }

        state.SetTop(0);
        return results;
    }

    public static LuauValue[] DoString(LuauState state, string source, string? chunkName = null)
    {
        if (chunkName is null)
        {
            return DoString(state, Encoding.UTF8.GetBytes(source));
        }

        Span<byte> chunkNameBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(chunkName.Length)];
        var chunkNameLength = Encoding.UTF8.GetBytes(chunkName, chunkNameBytes);
        return DoString(state, Encoding.UTF8.GetBytes(source), chunkNameBytes[..chunkNameLength]);
    }
}
