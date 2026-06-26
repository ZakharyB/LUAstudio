using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.ExecutionHost.Runtime;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;
using Xunit;

namespace LUAstudio.Tests;

public sealed class ExecutionSandboxTests
{
    [Fact]
    public async Task Interpreter_executes_print_without_touching_host()
    {
        var debug = new DebugController();
        var env = new SandboxEnvironment(robloxMocks: true);
        var interpreter = new InstrumentedAstInterpreter(env, debug);
        string? output = null;
        interpreter.Output += (_, text) => output = text;

        var source = "print(\"hello sandbox\")";
        var snapshot = new SourceSnapshot(Guid.NewGuid(), 1, SourceText.From(source), "test.lua", LuaDialect.Luau);
        var parseResult = await new LuaParserService().ParseDocumentAsync(snapshot);
        var unit = (CompilationUnitSyntax)parseResult.Tree.Root;

        await interpreter.ExecuteAsync(unit, source, "test.lua", CancellationToken.None);

        Assert.Equal("hello sandbox", output);
    }

    [Fact]
    public async Task Breakpoint_pauses_execution()
    {
        var debug = new DebugController();
        debug.SetBreakpoints("test.lua", [new BreakpointSpec(2)]);
        var env = new SandboxEnvironment(robloxMocks: false);
        var interpreter = new InstrumentedAstInterpreter(env, debug);

        var source = "local x = 1\nlocal y = 2\n";
        var snapshot = new SourceSnapshot(Guid.NewGuid(), 1, SourceText.From(source), "test.lua", LuaDialect.Luau);
        var parseResult = await new LuaParserService().ParseDocumentAsync(snapshot);
        var unit = (CompilationUnitSyntax)parseResult.Tree.Root;

        var hit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        debug.BreakpointHit += (_, _) => hit.TrySetResult(true);

        var execution = interpreter.ExecuteAsync(unit, source, "test.lua", CancellationToken.None);
        await Task.WhenAny(hit.Task, Task.Delay(1000));
        debug.Continue();
        await execution;

        Assert.True(hit.Task.IsCompletedSuccessfully);
    }
}
