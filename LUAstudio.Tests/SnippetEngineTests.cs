using LUAstudio.IntelliSense.Completion;
using Xunit;

namespace LUAstudio.Tests;

public sealed class SnippetEngineTests
{
    [Fact]
    public void Expand_replaces_placeholders()
    {
        var result = SnippetEngine.Expand("function ${1:name}()\n\t${0}\nend");
        Assert.Equal("function name()\n\t\nend", result.Text);
        Assert.Equal(2, result.Placeholders.Count);
    }

    [Fact]
    public void GetDisplayText_strips_placeholders()
    {
        var display = SnippetEngine.GetDisplayText("local ${1:x} = ${0}");
        Assert.Equal("local x = ", display);
    }
}
