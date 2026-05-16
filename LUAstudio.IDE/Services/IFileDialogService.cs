namespace LUAstudio.IDE.Services;

public interface IFileDialogService
{
    string? ShowOpenFileDialog();

    string? ShowSaveFileDialog(string? suggestedFileName);

    string? ShowOpenFolderDialog();
}
