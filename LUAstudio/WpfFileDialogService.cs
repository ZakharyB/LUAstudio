using LUAstudio.IDE.Services;
using Microsoft.Win32;

namespace LUAstudio;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Lua source (*.lua)|*.lua|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? ShowSaveFileDialog(string? suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Lua source (*.lua)|*.lua|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "untitled.lua" : suggestedFileName,
            RestoreDirectory = true,
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? ShowOpenFolderDialog()
    {
        var dlg = new OpenFolderDialog
        {
            Multiselect = false,
        };

        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
