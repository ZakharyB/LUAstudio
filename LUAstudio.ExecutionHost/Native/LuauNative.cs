namespace LUAstudio.ExecutionHost.Native;

/// <summary>
/// Native Luau VM bindings are provided by the <c>Luau.Native</c> NuGet package.
/// <see cref="Luau.Native.NativeMethods"/> exposes the C API used by <see cref="Debugging.LuauDebugController"/>.
/// </summary>
public static class LuauNative
{
    public const string LibraryName = "libluau";
}
