using System.Runtime.InteropServices;

namespace LUAstudio.ExecutionHost.Runtime;

/// <summary>
/// Luau.Native 0.1.6 imports malloc/free from libluau, but the shipped native library does not export them.
/// Route deallocation through the Windows CRT instead.
/// </summary>
internal static unsafe class LuauNativeMemory
{
    [DllImport("ucrtbase.dll", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void Free(void* ptr);
}
