using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.Core.Logging;
using LUAstudio.Core.Threading;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Explorer;
using LUAstudio.IDE.Services;
using LUAstudio.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LUAstudio.IDE.ViewModels;

public sealed partial class WorkspaceExplorerViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspace;
    private readonly IDocumentService _documents;
    private readonly IFileDialogService _fileDialogs;
    private readonly IAppLogger _logger;
    private readonly IUserPromptService _prompts;
    private readonly IExplorerShellService _shell;
    private readonly IExplorerNodeDecorationProvider _decorations;
    private readonly IMainThread _mainThread;
    private CancellationTokenSource? _filterDebounceCts;

    public WorkspaceExplorerViewModel(
        IWorkspaceService workspace,
        IDocumentService documents,
        IFileDialogService fileDialogs,
        IAppLogger logger,
        IUserPromptService prompts,
        IExplorerShellService shell,
        IExplorerNodeDecorationProvider decorations,
        IGitDecorationProvider gitDecorations,
        IMainThread mainThread)
    {
        _workspace = workspace;
        _documents = documents;
        _fileDialogs = fileDialogs;
        _logger = logger;
        _prompts = prompts;
        _shell = shell;
        _decorations = decorations;
        _mainThread = mainThread;
        gitDecorations.DecorationsChanged += (_, _) =>
            _mainThread.Send(() => _decorations.RefreshAll(RootNodes));
        RootNodes.CollectionChanged += OnRootNodesChanged;
        _documents.Documents.CollectionChanged += OnDocumentsCollectionChanged;
        foreach (var doc in _documents.Documents)
        {
            doc.PropertyChanged += OnDocumentPropertyChanged;
        }
    }

    public ObservableCollection<FileSystemEntryNode> RootNodes => _workspace.RootNodes;

    public int RootNodeCount => RootNodes.Count;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isFilterActive;

    partial void OnFilterTextChanged(string value)
    {
        _ = DebouncedApplyFilterAsync();
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RootNodeCount));
        _decorations.RefreshAll(RootNodes);
        _ = DebouncedApplyFilterAsync();
    }

    private void OnDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TextDocument doc in e.OldItems)
            {
                doc.PropertyChanged -= OnDocumentPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TextDocument doc in e.NewItems)
            {
                doc.PropertyChanged += OnDocumentPropertyChanged;
            }
        }

        _decorations.RefreshAll(RootNodes);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TextDocument doc)
        {
            return;
        }

        if (e.PropertyName is nameof(TextDocument.IsDirty) or nameof(TextDocument.FilePath))
        {
            _decorations.RefreshPath(doc.FilePath, RootNodes);
        }
    }

    [RelayCommand]
    private async Task AddWorkspaceRootAsync()
    {
        var path = _fileDialogs.ShowOpenFolderDialog();
        if (path is null)
        {
            return;
        }

        try
        {
            await _workspace.AddRootAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Add workspace root failed: {ex.Message}");
            _prompts.ShowError($"Could not add folder:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveWorkspaceRootAsync(FileSystemEntryNode? node)
    {
        if (node is null || !node.IsWorkspaceRoot)
        {
            return;
        }

        try
        {
            await _workspace.RemoveRootAsync(node.FullPath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Remove workspace root failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MoveWorkspaceRootUpAsync(FileSystemEntryNode? node)
    {
        await MoveWorkspaceRootByOffsetAsync(node, -1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveWorkspaceRootDownAsync(FileSystemEntryNode? node)
    {
        await MoveWorkspaceRootByOffsetAsync(node, 1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenEntryAsync(FileSystemEntryNode? node)
    {
        if (node is null || node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return;
        }

        try
        {
            var autoSwitch = Engine.Globals.Get<bool>(SettingKeys.EditorAutoSwitchOnOpen)?.Value ?? true;
            await _documents.OpenFromPathAsync(node.FullPath, switchToDocument: autoSwitch).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Open from explorer failed: {ex.Message}");
            _prompts.ShowError($"Could not open file:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshWorkspaceTree()
    {
        foreach (var root in RootNodes)
        {
            ClearLoadedChildrenRecursive(root);
        }

        _decorations.RefreshAll(RootNodes);
        _ = DebouncedApplyFilterAsync();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private async Task TreeItemExpandedAsync(FileSystemEntryNode? node)
    {
        if (node is null || !node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return;
        }

        try
        {
            await _workspace.EnsureChildrenLoadedAsync(node).ConfigureAwait(true);
            _decorations.RefreshAll(RootNodes);
            if (IsFilterActive)
            {
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Load directory children failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NewFileAsync(FileSystemEntryNode? contextNode)
    {
        var parentDir = ResolveParentDirectory(contextNode);
        if (parentDir is null)
        {
            return;
        }

        var name = _prompts.PromptForText("New File", "File name:", "untitled.lua");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var path = Path.Combine(parentDir, name.Trim());
        try
        {
            if (File.Exists(path))
            {
                _prompts.ShowError("A file with that name already exists.");
                return;
            }

            await File.WriteAllTextAsync(path, string.Empty).ConfigureAwait(true);
            await InvalidateAndOpenAsync(parentDir, path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Could not create file:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NewFolderAsync(FileSystemEntryNode? contextNode)
    {
        var parentDir = ResolveParentDirectory(contextNode);
        if (parentDir is null)
        {
            return;
        }

        var name = _prompts.PromptForText("New Folder", "Folder name:", "NewFolder");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var path = Path.Combine(parentDir, name.Trim());
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                _prompts.ShowError("An item with that name already exists.");
                return;
            }

            Directory.CreateDirectory(path);
            await InvalidateParentAsync(parentDir).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Could not create folder:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RenameEntryAsync(FileSystemEntryNode? node)
    {
        if (node is null || node.IsTruncationPlaceholder || node.IsWorkspaceRoot)
        {
            return;
        }

        var newName = _prompts.PromptForText("Rename", "New name:", node.DisplayName);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), node.DisplayName, StringComparison.Ordinal))
        {
            return;
        }

        var parent = Path.GetDirectoryName(node.FullPath);
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        var target = Path.Combine(parent, newName.Trim());
        try
        {
            if (node.IsDirectory)
            {
                if (Directory.Exists(target))
                {
                    _prompts.ShowError("A folder with that name already exists.");
                    return;
                }

                Directory.Move(node.FullPath, target);
            }
            else
            {
                if (File.Exists(target))
                {
                    _prompts.ShowError("A file with that name already exists.");
                    return;
                }

                File.Move(node.FullPath, target);
            }

            await InvalidateParentAsync(parent).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Could not rename:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(FileSystemEntryNode? node)
    {
        if (node is null || node.IsTruncationPlaceholder || node.IsWorkspaceRoot)
        {
            return;
        }

        var label = node.IsDirectory ? "folder" : "file";
        if (!_prompts.Confirm("Delete", $"Delete {label} \"{node.DisplayName}\"? This cannot be undone."))
        {
            return;
        }

        var parent = Path.GetDirectoryName(node.FullPath);
        try
        {
            if (node.IsDirectory)
            {
                Directory.Delete(node.FullPath, recursive: true);
            }
            else
            {
                File.Delete(node.FullPath);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                await InvalidateParentAsync(parent).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _prompts.ShowError($"Could not delete:{Environment.NewLine}{ex.Message}");
        }
    }

    [RelayCommand]
    private void RevealInExplorer(FileSystemEntryNode? node)
    {
        if (node is null || node.IsTruncationPlaceholder)
        {
            return;
        }

        _shell.RevealInExplorer(node.FullPath);
    }

    [RelayCommand]
    private void CopyPath(FileSystemEntryNode? node)
    {
        if (node is null || node.IsTruncationPlaceholder)
        {
            return;
        }

        _shell.CopyPathToClipboard(node.FullPath);
    }

    [RelayCommand]
    private void OpenInTerminal(FileSystemEntryNode? node)
    {
        var dir = node switch
        {
            null => null,
            { IsTruncationPlaceholder: true } => null,
            { IsDirectory: true } => node.FullPath,
            _ => Path.GetDirectoryName(node.FullPath),
        };

        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        _shell.OpenInTerminal(dir);
    }

    public async Task ReorderRootsAsync(int fromIndex, int toIndex)
    {
        try
        {
            await _workspace.MoveRootAsync(fromIndex, toIndex).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Reorder workspace root failed: {ex.Message}");
        }
    }

    public IReadOnlyList<FileSystemEntryNode> GetFilterExpandNodes()
    {
        return ExplorerFilterEngine.CollectExpandPathNodes(RootNodes, FilterText);
    }

    public void ApplyFilter()
    {
        var active = !string.IsNullOrWhiteSpace(FilterText);
        IsFilterActive = active;
        if (!active)
        {
            ExplorerFilterEngine.ClearFilter(RootNodes);
            return;
        }

        ExplorerFilterEngine.ApplyFilter(RootNodes, FilterText);
    }

    private async Task DebouncedApplyFilterAsync()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;
        try
        {
            await Task.Delay(180, token).ConfigureAwait(true);
            if (IsFilterActive || !string.IsNullOrWhiteSpace(FilterText))
            {
                await LoadTreeForFilterAsync(token).ConfigureAwait(true);
            }

            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // superseded by newer filter input
        }
    }

    private async Task LoadTreeForFilterAsync(CancellationToken cancellationToken)
    {
        var pattern = FilterText.Trim();
        if (pattern.Length == 0)
        {
            return;
        }

        foreach (var root in RootNodes.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadMatchingSubtreeAsync(root, pattern, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task LoadMatchingSubtreeAsync(FileSystemEntryNode node, string pattern, CancellationToken cancellationToken)
    {
        if (!node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return;
        }

        if (!node.IsChildrenLoaded)
        {
            await _workspace.EnsureChildrenLoadedAsync(node, cancellationToken).ConfigureAwait(true);
        }

        foreach (var child in node.Children.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.IsTruncationPlaceholder)
            {
                continue;
            }

            var childMatches = FuzzyExplorerMatcher.TryMatch(child.DisplayName, pattern, out _);
            if (child.IsDirectory)
            {
                if (childMatches || MayContainMatch(child, pattern))
                {
                    await LoadMatchingSubtreeAsync(child, pattern, cancellationToken).ConfigureAwait(true);
                }
            }
        }
    }

    private static bool MayContainMatch(FileSystemEntryNode directory, string pattern)
    {
        // Heuristic: load branch when directory name shares first character(s) with pattern.
        if (pattern.Length == 0)
        {
            return true;
        }

        return FuzzyExplorerMatcher.TryMatch(directory.DisplayName, pattern, out _) ||
               directory.DisplayName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private async Task InvalidateAndOpenAsync(string parentDir, string filePath)
    {
        await InvalidateParentAsync(parentDir).ConfigureAwait(true);
        await _documents.OpenFromPathAsync(filePath).ConfigureAwait(true);
    }

    private async Task InvalidateParentAsync(string parentDir)
    {
        var parentNode = FindFolderNode(parentDir);
        if (parentNode is not null)
        {
            ClearLoadedChildrenRecursive(parentNode);
            await _workspace.EnsureChildrenLoadedAsync(parentNode).ConfigureAwait(true);
        }

        _decorations.RefreshAll(RootNodes);
        ApplyFilter();
    }

    private FileSystemEntryNode? FindFolderNode(string directoryFullPath)
    {
        foreach (var root in RootNodes)
        {
            var found = FindFolderRecursive(root, directoryFullPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static FileSystemEntryNode? FindFolderRecursive(FileSystemEntryNode node, string directoryFullPath)
    {
        if (string.Equals(node.FullPath, directoryFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        if (!node.IsChildrenLoaded)
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            if (!child.IsDirectory || child.IsTruncationPlaceholder)
            {
                continue;
            }

            var found = FindFolderRecursive(child, directoryFullPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string? ResolveParentDirectory(FileSystemEntryNode? contextNode)
    {
        if (contextNode is null)
        {
            return null;
        }

        if (contextNode.IsTruncationPlaceholder)
        {
            return null;
        }

        if (contextNode.IsDirectory)
        {
            return contextNode.FullPath;
        }

        return Path.GetDirectoryName(contextNode.FullPath);
    }

    private async Task MoveWorkspaceRootByOffsetAsync(FileSystemEntryNode? node, int offset)
    {
        if (node is null || !node.IsWorkspaceRoot)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < RootNodes.Count; i++)
        {
            if (ReferenceEquals(RootNodes[i], node))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        var target = index + offset;
        if (target < 0 || target >= RootNodes.Count)
        {
            return;
        }

        await ReorderRootsAsync(index, target).ConfigureAwait(true);
    }

    private static void ClearLoadedChildrenRecursive(FileSystemEntryNode node)
    {
        if (!node.IsDirectory)
        {
            return;
        }

        node.Children.Clear();
        node.IsChildrenLoaded = false;
        node.LoadError = null;
    }
}
