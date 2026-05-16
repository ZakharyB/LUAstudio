using System.Windows;
using LUAstudio.IDE.Services;

namespace LUAstudio;

public sealed class WpfUserPromptService : IUserPromptService
{
    public SavePromptResult AskSaveChanges(string documentDisplayName)
    {
        var result = MessageBox.Show(
            $"""Save changes to "{documentDisplayName}"?""",
            "LuaStudio",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => SavePromptResult.Save,
            MessageBoxResult.No => SavePromptResult.Discard,
            _ => SavePromptResult.Cancel,
        };
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "LuaStudio", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public string? PromptForText(string title, string message, string defaultValue = "")
    {
        var owner = Application.Current?.MainWindow;
        var dialog = new TextInputDialog(title, message, defaultValue)
        {
            Owner = owner,
        };

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
