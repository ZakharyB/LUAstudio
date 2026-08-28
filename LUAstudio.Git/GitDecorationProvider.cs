using LUAstudio.IDE.Services;

namespace LUAstudio.Git;

public sealed class GitDecorationProvider : IGitDecorationProvider
{
    private readonly IGitStatusService _status;

    public GitDecorationProvider(IGitStatusService status)
    {
        _status = status;
        _status.StatusChanged += (_, _) => DecorationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DecorationsChanged;

    public string? GetGlyph(string fullPath) => Find(fullPath)?.Glyph;

    public string? GetToolTip(string fullPath)
    {
        var file = Find(fullPath);
        return file is null ? null : $"Git: {file.State}{(file.IsStaged ? " (staged)" : string.Empty)}";
    }

    private GitFileStatus? Find(string fullPath) => _status.Files.FirstOrDefault(file =>
        string.Equals(file.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
}
