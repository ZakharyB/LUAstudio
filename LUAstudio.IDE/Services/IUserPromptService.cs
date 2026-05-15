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
}
