namespace LUAstudio;

public interface IDebugEditorNavigation
{
    void NavigateTo(string? sourcePath, int line, int column = 1);
}
