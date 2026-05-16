namespace LUAstudio.IDE.Services;

public enum SavePromptResult
{
    Save,
    Discard,
    Cancel,
}

public interface IUserPromptService
{
    SavePromptResult AskSaveChanges(string documentDisplayName);

    void ShowError(string message);

    string? PromptForText(string title, string message, string defaultValue = "");

    bool Confirm(string title, string message);
}
