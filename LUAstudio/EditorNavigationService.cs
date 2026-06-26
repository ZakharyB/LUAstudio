namespace LUAstudio;

public sealed class EditorNavigationService
{
    public string? SourcePath { get; private set; }

    public int Line { get; private set; }

    public int Column { get; private set; }

    public event Action? NavigationRequested;

    public void NavigateTo(string? sourcePath, int line, int column = 1)
    {
        SourcePath = sourcePath;
        Line = line;
        Column = column;
        NavigationRequested?.Invoke();
    }
}
