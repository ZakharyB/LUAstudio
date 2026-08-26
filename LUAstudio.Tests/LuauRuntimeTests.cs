using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.ExecutionHost.Runtime;
using Luau;
using Xunit;

namespace LUAstudio.Tests;

public sealed class LuauRuntimeTests
{
    [Fact]
    public async Task LuauRuntime_executes_print_on_worker_thread_without_host_leakage()
    {
        var debug = new LuauDebugController();
        var modules = new ModuleResolver();
        using var runtime = new LuauRuntime(debug, modules, new SessionConfiguration());
        string? output = null;
        runtime.Output += (_, text) => output = text;
        runtime.Initialize(enableRobloxMocks: true);
        runtime.LoadScript("print(\"hello sandbox\")", "test.lua");
        // Sessions initialize on the pipe thread and execute on a worker thread.
        // Output routing must be keyed to lua_State rather than thread-local state.
        await Task.Run(() => runtime.Execute(CancellationToken.None));

        Assert.Equal("hello sandbox", output);
    }

    [Fact]
    public void LuauRuntime_supports_tables_and_functions()
    {
        var debug = new LuauDebugController();
        var modules = new ModuleResolver();
        using var runtime = new LuauRuntime(debug, modules, new SessionConfiguration());
        runtime.Initialize(enableRobloxMocks: false);
        runtime.LoadScript(
            """
            local function add(a, b)
                return a + b
            end
            local t = { x = add(1, 2) }
            assert(t.x == 3)
            """,
            "math.lua");
        runtime.Execute(CancellationToken.None);
    }
}

public sealed class ModuleResolverTests
{
    [Fact]
    public void ModuleResolver_resolves_in_memory_modules()
    {
        var resolver = new ModuleResolver();
        resolver.SetModule("ModuleA", "return { value = 42 }");

        Assert.True(resolver.TryGetSource("ModuleA", out var source));
        Assert.Contains("42", source);
    }
}

public sealed class ExecutionTraceRecorderTests
{
    [Fact]
    public void ExecutionTraceRecorder_records_mock_calls()
    {
        var recorder = new ExecutionTraceRecorder();
        recorder.RecordMockCall("Instance.new", "Part");
        var snapshot = recorder.CreateSnapshot(Guid.NewGuid(), "hash", 1);

        Assert.Single(snapshot.Events);
        Assert.Equal("mock", snapshot.Events[0].Kind);
    }
}
