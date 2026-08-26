using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using Luau;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class LuauSandboxBootstrap
{
    private readonly ExecutionTraceRecorder? _trace;

    public LuauSandboxBootstrap(ExecutionTraceRecorder? trace = null) => _trace = trace;

    public unsafe void Configure(LuauState state, bool enableRobloxMocks, Action<string, string>? output)
    {
        SandboxNativeBindings.Register(state.AsPointer(), output, _trace);
        state.OpenBaseLibrary();
        state.OpenMathLibrary();
        state.OpenTableLibrary();
        state.OpenStringLibrary();
        state.OpenCoroutineLibrary();
        state.OpenBit32Library();
        state.OpenUtf8Library();
        state.OpenBufferLibrary();
        state.OpenVectorLibrary();
        // Deliberately do not expose the debug library. Debugging is implemented by
        // the host through the native API; guest code must not inspect or mutate the
        // host-managed execution state.

        RegisterNativeGlobal(state, "__sandbox_print", SandboxNativeBindings.Print);
        LuauScriptRunner.DoString(
            state,
            """
            -- Remove dynamic-code and environment escape hatches from guest code.
            loadstring = nil
            getfenv = nil
            setfenv = nil

            print = function(...)
                local parts = {...}
                local buffer = {}
                for i = 1, #parts do
                    buffer[i] = tostring(parts[i])
                end
                __sandbox_print(table.concat(buffer, "	"))
            end
            """);

        if (enableRobloxMocks)
        {
            SeedRobloxMocks(state);
        }
    }

    private static unsafe void SeedRobloxMocks(LuauState state)
    {
        RegisterNativeGlobal(state, "__sandbox_instance_new", SandboxNativeBindings.InstanceNew);
        LuauScriptRunner.DoString(
            state,
            """
            game = { Name = "Game" }
            workspace = { Name = "Workspace" }
            script = { Name = "Script" }
            Instance = {
                new = function(className)
                    return __sandbox_instance_new(className)
                end
            }
            """);
    }

    private static unsafe void RegisterNativeGlobal(LuauState state, string name, lua_CFunction callback)
    {
        var L = state.AsPointer();
        state.PushCFunction(callback);
        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* namePtr = bytes)
        {
            lua_setglobal(L, namePtr);
        }
    }
}

internal static unsafe class SandboxNativeBindings
{
    private sealed record StateBindings(Action<string, string>? Output, ExecutionTraceRecorder? Trace);
    private static readonly ConcurrentDictionary<nint, StateBindings> Bindings = new();

    public static void Register(lua_State* state, Action<string, string>? output, ExecutionTraceRecorder? trace) =>
        Bindings[(nint)state] = new StateBindings(output, trace);

    public static void Unregister(lua_State* state) => Bindings.TryRemove((nint)state, out _);

    public static int Print(lua_State* L)
    {
        var top = lua_gettop(L);
        var parts = new List<string>(top);
        for (var index = 1; index <= top; index++)
        {
            var valuePtr = lua_tostring(L, index);
            parts.Add(valuePtr == null ? "nil" : Marshal.PtrToStringUTF8((IntPtr)valuePtr) ?? "nil");
        }

        if (Bindings.TryGetValue((nint)L, out var bindings))
        {
            bindings.Output?.Invoke("stdout", string.Join('\t', parts));
        }
        return 0;
    }

    public static int InstanceNew(lua_State* L)
    {
        var classNamePtr = lua_tostring(L, 1);
        var className = classNamePtr == null ? "Part" : Marshal.PtrToStringUTF8((IntPtr)classNamePtr) ?? "Part";
        if (Bindings.TryGetValue((nint)L, out var bindings))
        {
            bindings.Trace?.RecordMockCall("Instance.new", className);
        }

        lua_newtable(L);
        PushLiteral(L, "ClassName");
        PushLiteral(L, className);
        lua_settable(L, -3);
        PushLiteral(L, "Name");
        PushLiteral(L, className);
        lua_settable(L, -3);
        return 1;
    }

    private static void PushLiteral(lua_State* L, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + "\0");
        fixed (byte* ptr = bytes)
        {
            lua_pushstring(L, ptr);
        }
    }
}

internal static class LuauValueFormatter
{
    public static string Format(LuauValue value) => value.Type switch
    {
        LuauType.Nil => "nil",
        LuauType.Boolean => value.Read<bool>() ? "true" : "false",
        LuauType.Number => value.Read<double>().ToString(System.Globalization.CultureInfo.InvariantCulture),
        LuauType.String => value.Read<string>(),
        LuauType.Table => "{...}",
        LuauType.Funciton => "function",
        _ => value.ToString() ?? "nil"
    };
}
