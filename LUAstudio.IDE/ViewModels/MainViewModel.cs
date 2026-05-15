using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Services;

namespace LUAstudio.IDE.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDocumentService _documents;
    private readonly IFileDialogService _fileDialogs;
    private readonly IUserPromptService _prompts;
    private TextDocument? _hookedDocument;

    public MainViewModel(IDocumentService documents, IFileDialogService fileDialogs, IUserPromptService prompts)
    {
        _documents = documents;
        _fileDialogs = fileDialogs;
        _prompts = prompts;

        _documents.PropertyChanged += OnDocumentsPropertyChanged;
        HookActiveDocument(_documents.ActiveDocument);
    }

    public ObservableCollection<TextDocument> OpenDocuments => _documents.Documents;

    public TextDocument? ActiveDocument
    {
        get => _documents.ActiveDocument;
        set => _documents.ActiveDocument = value;
    }

    public string WindowTitle
    {
        get
        {
            var doc = _documents.ActiveDocument;
            return doc is null ? "LuaStudio" : $"LuaStudio - {doc.DisplayName}{(doc.IsDirty ? " *" : string.Empty)}";
        }
    }

    [RelayCommand]
    private void NewDocument() => _documents.CreateUntitled();

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        var path = _fileDialogs.ShowOpenFileDialog();
        if (path is null)
        {
            return;
        }

        try
        {
            await _documents.OpenFromPathAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Could not open file:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveActiveAsync()
    {
        if (ActiveDocument is null)
        {
            return;
        }

        try
        {
            await SaveInternalAsync(ActiveDocument).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Save dialog cancelled.
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Save failed:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveAsActiveAsync()
    {
        if (ActiveDocument is null)
        {
            return;
        }

        var path = _fileDialogs.ShowSaveFileDialog(ActiveDocument.DisplayName);
        if (path is null)
        {
            return;
        }

        try
        {
            await _documents.SaveAsAsync(ActiveDocument, path).ConfigureAwait(true);
            OnPropertyChanged(nameof(WindowTitle));
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Save failed:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private Task CloseActiveAsync() => CloseInternalAsync(ActiveDocument);

    [RelayCommand]
    private Task CloseDocumentAsync(TextDocument? doc) => CloseInternalAsync(doc);

    private async Task CloseInternalAsync(TextDocument? doc)
    {
        doc ??= ActiveDocument;
        if (doc is null)
        {
            return;
        }

        if (doc.IsDirty)
        {
            var r = _prompts.AskSaveChanges(doc.DisplayName);
            if (r == SavePromptResult.Cancel)
            {
                return;
            }

            if (r == SavePromptResult.Save)
            {
                try
                {
                    await SaveInternalAsync(doc).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _prompts.ShowError($"Save failed:{Environment.NewLine}{ex.Message}");
                    return;
                }
            }
        }

        _documents.RemoveDocument(doc);
    }

    private async Task SaveInternalAsync(TextDocument doc)
    {
        if (doc.FilePath is null)
        {
            var path = _fileDialogs.ShowSaveFileDialog(doc.DisplayName);
            if (path is null)
            {
                throw new OperationCanceledException();
            }

            await _documents.SaveAsAsync(doc, path).ConfigureAwait(true);
            OnPropertyChanged(nameof(WindowTitle));
            return;
        }

        await _documents.SaveAsync(doc).ConfigureAwait(true);
    }

    private void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDocumentService.ActiveDocument))
        {
            HookActiveDocument(_documents.ActiveDocument);
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void HookActiveDocument(TextDocument? doc)
    {
        if (ReferenceEquals(_hookedDocument, doc))
        {
            return;
        }

        if (_hookedDocument is not null)
        {
            _hookedDocument.PropertyChanged -= OnHookedDocumentPropertyChanged;
        }

        _hookedDocument = doc;
        if (_hookedDocument is not null)
        {
            _hookedDocument.PropertyChanged += OnHookedDocumentPropertyChanged;
        }

        OnPropertyChanged(nameof(WindowTitle));
    }

    private void OnHookedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TextDocument.DisplayName) or nameof(TextDocument.IsDirty))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
