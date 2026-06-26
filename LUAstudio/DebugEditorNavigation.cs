using LUAstudio.IDE.Documents;
using LUAstudio.IDE.ViewModels;

namespace LUAstudio;

public sealed class DebugEditorNavigation : IDebugEditorNavigation
{
    private readonly IDocumentService _documents;
    private readonly MainViewModel _mainViewModel;
    private readonly EditorNavigationService _navigation;

    public DebugEditorNavigation(
        IDocumentService documents,
        MainViewModel mainViewModel,
        EditorNavigationService navigation)
    {
        _documents = documents;
        _mainViewModel = mainViewModel;
        _navigation = navigation;
    }

    public void NavigateTo(string? sourcePath, int line, int column = 1)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        var document = _documents.Documents.FirstOrDefault(d =>
            string.Equals(d.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase));

        if (document is not null)
        {
            _mainViewModel.ActiveDocument = document;
        }

        _mainViewModel.UpdateCaretPosition(line, column);
        _navigation.NavigateTo(sourcePath, line, column);
    }
}
