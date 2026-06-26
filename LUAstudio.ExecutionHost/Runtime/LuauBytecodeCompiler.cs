using Luau;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace LUAstudio.ExecutionHost.Runtime;

internal static unsafe class LuauBytecodeCompiler
{
    private static readonly lua_CompileOptions DefaultOptions = new()
    {
        debugLevel = 1,
        optimizationLevel = 1,
        typeInfoLevel = 1,
        coverageLevel = 2,
    };

    public static byte[] Compile(ReadOnlySpan<byte> source)
    {
        byte* code;
        nuint size;
        var options = DefaultOptions;

        fixed (byte* ptr = source)
        {
            code = luau_compile(ptr, (nuint)source.Length, &options, &size);
        }

        try
        {
            if (size > 0x7FFFFFC7)
            {
                throw new LuauException("Bytecode size is too large.");
            }

            var result = new byte[(int)size];
            new ReadOnlySpan<byte>(code, (int)size).CopyTo(result);
            return result;
        }
        finally
        {
            if (code != null)
            {
                LuauNativeMemory.Free(code);
            }
        }
    }
}
