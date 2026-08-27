using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;
using Xunit;

namespace LUAstudio.Tests;

public sealed class LuaParserTests
{
    [Fact]
    public async Task Parse_function_does_not_stack_overflow()
    {
        var parser = new LuaParserService();

        var snapshot = new SourceSnapshot(
            Guid.NewGuid(),
            1,
            SourceText.From(
                "local function foo()\n  print(1)\nend"),
            null,
            LuaDialect.Lua);

        var result =
            await parser.ParseDocumentAsync(snapshot);

        Assert.Contains(
            result.Tree.Root.DescendantsAndSelf(),
            node => node is FunctionDeclarationSyntax);
    }

    [Fact]
    public async Task Parse_if_statement()
    {
        var parser = new LuaParserService();

        var snapshot = new SourceSnapshot(
            Guid.NewGuid(),
            1,
            SourceText.From(
                "if true then\n  print(1)\nend"),
            null,
            LuaDialect.Lua);

        var result =
            await parser.ParseDocumentAsync(snapshot);

        Assert.Contains(
            result.Tree.Root.DescendantsAndSelf(),
            node => node is IfStatementSyntax);
    }

    [Fact]
    public async Task Parse_local_function_with_non_breaking_space_indentation_has_no_errors()
    {
        const string source =
            "local x = 5\n" +
            "local y = 12\n" +
            "\n" +
            "local function calculate(x, y)\n" +
            "\u00A0\u00A0\u00A0\u00A0return x + y\n" +
            "end\n" +
            "\n" +
            "local result = calculate(x, y)\n";

        var parser = new LuaParserService();

        var snapshot = new SourceSnapshot(
            Guid.NewGuid(),
            1,
            SourceText.From(source),
            "sample.lua",
            LuaDialect.Lua);

        var result =
            await parser.ParseDocumentAsync(snapshot);

        Assert.Empty(result.Tree.Diagnostics);

        Assert.Contains(
            result.Tree.Root.DescendantsAndSelf(),
            node => node is FunctionDeclarationSyntax);
    }
}