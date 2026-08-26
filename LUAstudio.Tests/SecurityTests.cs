using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.ExecutionHost.Runtime;
using Xunit;

namespace LUAstudio.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void LuauRuntime_does_not_expose_io_library()
    {
        var debug = new LuauDebugController();
        var modules = new ModuleResolver();
        using var runtime = new LuauRuntime(debug, modules, new SessionConfiguration());
        runtime.Initialize(enableRobloxMocks: false);
        runtime.LoadScript(
            """
            local ok = pcall(function()
                local _ = io.open
            end)
            assert(ok == false)
            """,
            "security.lua");

        runtime.Execute(CancellationToken.None);
    }

    [Fact]
    public void LuauRuntime_hides_debug_and_dynamic_environment_apis()
    {
        var debug = new LuauDebugController();
        var modules = new ModuleResolver();
        using var runtime = new LuauRuntime(debug, modules, new SessionConfiguration());
        runtime.Initialize(enableRobloxMocks: false);
        runtime.LoadScript(
            """
            assert(debug == nil)
            assert(loadstring == nil)
            assert(getfenv == nil)
            assert(setfenv == nil)
            """,
            "security.lua");

        runtime.Execute(CancellationToken.None);
    }
}
