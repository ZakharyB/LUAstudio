using LUAstudio.Editor.Editing;
using Xunit;

namespace LUAstudio.Tests;

public sealed class BlockStructureServiceTests
{
    [Theory]
    [InlineData("function render()")]
    [InlineData("local function sendMessage()")]
    public void Function_names_containing_end_still_open_a_block(string source)
    {
        var block = BlockStructureService.GetBlockAfterCaret(source, source.Length, root: null);

        Assert.NotNull(block);
        Assert.Equal("function", block.Keyword);
    }
}
