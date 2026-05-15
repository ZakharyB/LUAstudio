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
}
