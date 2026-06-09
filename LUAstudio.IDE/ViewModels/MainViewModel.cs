using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.Core.Logging;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Services;
using LUAstudio.Workspace;

namespace LUAstudio.IDE.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IFileSystemActivitySink
{
    private readonly IDocumentService _documents;
    private readonly IFileDialogService _fileDialogs;
    private readonly IUserPromptService _prompts;
    private readonly IWorkspaceService _workspace;
    private readonly IAppLogger _logger;
    private TextDocument? _hookedDocument;

    public MainViewModel(
        IDocumentService documents,
        IFileDialogService fileDialogs,
        IUserPromptService prompts,
        IWorkspaceService workspace,
        IAppLogger logger,
        WorkspaceExplorerViewModel explorer)
    {
        _documents = documents;
        _fileDialogs = fileDialogs;
        _prompts = prompts;
        _workspace = workspace;
        _logger = logger;
        Explorer = explorer;

        _documents.PropertyChanged += OnDocumentsPropertyChanged;
        _documents.Documents.CollectionChanged += OnDocumentsCollectionChanged;
        HookActiveDocument(_documents.ActiveDocument);
        RefreshDocumentPresence();

        var restoreGlobal = Engine.Globals.Get<bool>(SettingKeys.RestoreWorkspaceRoots);
        if (restoreGlobal is not null)
        {
            restoreGlobal.Changed += value => RestoreWorkspaceOnStartup = value;
        }
    }

    public WorkspaceExplorerViewModel Explorer { get; }

    public ObservableCollection<TextDocument> OpenDocuments => _documents.Documents;

    public TextDocument? ActiveDocument
    {
        get => _documents.ActiveDocument;
        set => _documents.ActiveDocument = value;
    }

    [ObservableProperty]
    private string? _externalDiskMessage;

    [ObservableProperty]
    private bool _restoreWorkspaceOnStartup = true;

    [ObservableProperty]
    private bool _hasOpenDocuments;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private string _activeDocumentPath = string.Empty;

    public string WindowTitle
    {
        get
        {
            var doc = _documents.ActiveDocument;
            return doc is null ? "LuaStudio" : $"LuaStudio - {doc.DisplayName}{(doc.IsDirty ? " *" : string.Empty)}";
        }
    }

    public async Task InitializeAsync()
    {
        RestoreWorkspaceOnStartup = await _workspace.GetRestoreWorkspaceRootsAsync().ConfigureAwait(true);
        await _workspace.LoadAsync().ConfigureAwait(true);
    }

    void IFileSystemActivitySink.ReportFileSystemActivity(string? message)
    {
        ExternalDiskMessage = message;
    }

    public void UpdateCaretPosition(int line, int column)
    {
        CaretLine = line;
        CaretColumn = column;
    }

    private void OnDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDocumentPresence();
    }

    private void RefreshDocumentPresence()
    {
        HasOpenDocuments = OpenDocuments.Count > 0;
        ActiveDocumentPath = ActiveDocument?.FilePath ?? string.Empty;
    }

    partial void OnRestoreWorkspaceOnStartupChanged(bool value)
    {
        var global = Engine.Globals.Get<bool>(SettingKeys.RestoreWorkspaceRoots);
        if (global is not null && global.Value != value)
        {
            global.Value = value;
        }

        _ = SaveRestorePreferenceAsync(value);
    }

    private async Task SaveRestorePreferenceAsync(bool value)
    {
        try
        {
            await _workspace.SetRestoreWorkspaceRootsAsync(value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to save restore_workspace_roots: {ex.Message}");
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
            ActiveDocumentPath = ActiveDocument?.FilePath ?? string.Empty;
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
