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
        var snapshot = new SourceSnapshot(Guid.NewGuid(), 1, SourceText.From("local function foo()\n  print(1)\nend"), null, LuaDialect.Lua);
        var result = await parser.ParseDocumentAsync(snapshot);
        Assert.Contains(result.Tree.Root.DescendantsAndSelf(), n => n is FunctionDeclarationSyntax);
    }

    [Fact]
    public async Task Parse_if_statement()
    {
        var parser = new LuaParserService();
        var snapshot = new SourceSnapshot(Guid.NewGuid(), 1, SourceText.From("if true then\n  print(1)\nend"), null, LuaDialect.Lua);
        var result = await parser.ParseDocumentAsync(snapshot);
        Assert.Contains(result.Tree.Root.DescendantsAndSelf(), n => n is IfStatementSyntax);
    }
}
