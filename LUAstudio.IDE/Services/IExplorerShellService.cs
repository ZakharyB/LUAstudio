namespace LUAstudio.IDE.Services;

public interface IExplorerShellService
{
    void RevealInExplorer(string path);

    void OpenInTerminal(string directoryPath);

    void CopyPathToClipboard(string path);
}
