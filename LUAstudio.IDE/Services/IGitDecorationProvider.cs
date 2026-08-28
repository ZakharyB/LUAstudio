namespace LUAstudio.IDE.Services;

public interface IGitDecorationProvider
{
    event EventHandler? DecorationsChanged;

    string? GetGlyph(string fullPath);

    string? GetToolTip(string fullPath);
}

internal sealed class NullGitDecorationProvider : IGitDecorationProvider
{
    public event EventHandler? DecorationsChanged
    {
        add { }
        remove { }
    }

    public string? GetGlyph(string fullPath) => null;

    public string? GetToolTip(string fullPath) => null;
}
