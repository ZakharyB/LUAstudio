using LUAstudio.Execution.Abstractions;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;
using Xunit;

namespace LUAstudio.Tests;

public sealed class RequireGraphTests
{
    [Fact]
    public async Task Workspace_scan_records_string_literal_requires()
    {
        var temp = Path.Combine(Path.GetTempPath(), "luastudio-require-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.WriteAllText(Path.Combine(temp, "Main.lua"), "local M = require(\"Child\")\n");
        File.WriteAllText(Path.Combine(temp, "Child.lua"), "return {}\n");

        try
        {
            var moduleResolver = new ModuleResolver();
            moduleResolver.RebuildIndex([temp]);
            var requireGraph = new RequireGraphService();
            var scanner = new RequireGraphWorkspaceScanner(
                new LuaParserService(),
                new SemanticBinder(),
                moduleResolver,
                requireGraph,
                new NullEventBus());

            await scanner.ScanAsync([temp]);

            var edges = requireGraph.GetEdges();
            Assert.NotEmpty(edges);
            Assert.Contains(edges, e => e.ToModule.Contains("Child", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private sealed class NullEventBus : LUAstudio.Core.Events.IEventBus
    {
        public void Publish<T>(T message) where T : class { }
        public void Subscribe<T>(Action<T> handler) where T : class { }
        public void Unsubscribe<T>(Action<T> handler) where T : class { }
    }
}
