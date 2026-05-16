using System.Diagnostics;
using System.IO;
using System.Windows;
using LUAstudio.IDE.Services;

namespace LUAstudio;

public sealed class WpfExplorerShellService : IExplorerShellService
{
    public void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open Explorer:{Environment.NewLine}{ex.Message}", "LuaStudio",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OpenInTerminal(string directoryPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{directoryPath}\"") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/K cd /d \"{directoryPath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open terminal:{Environment.NewLine}{ex.Message}", "LuaStudio",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public void CopyPathToClipboard(string path)
    {
        try
        {
            Clipboard.SetText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not copy path:{Environment.NewLine}{ex.Message}", "LuaStudio",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
