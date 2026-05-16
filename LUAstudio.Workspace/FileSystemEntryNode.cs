using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LUAstudio.Workspace;

public partial class FileSystemEntryNode : ObservableObject
{
    public const int MaxChildrenPerDirectory = 2000;

    public FileSystemEntryNode(string fullPath, string displayName, bool isDirectory, bool isWorkspaceRoot)
    {
        FullPath = fullPath;
        DisplayName = displayName;
        IsDirectory = isDirectory;
        IsWorkspaceRoot = isWorkspaceRoot;
    }

    public string FullPath { get; }

    public string DisplayName { get; }

    public bool IsDirectory { get; }

    public bool IsWorkspaceRoot { get; }

    [ObservableProperty]
    private bool _isTruncationPlaceholder;

    [ObservableProperty]
    private bool _isChildrenLoaded;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private bool _isVisibleInFilter = true;

    [ObservableProperty]
    private bool _isFilterNameMatch;

    [ObservableProperty]
    private int[] _filterMatchIndices = Array.Empty<int>();

    [ObservableProperty]
    private string _filterMatchIndexCsv = string.Empty;

    partial void OnFilterMatchIndicesChanged(int[] value)
    {
        FilterMatchIndexCsv = value.Length == 0 ? string.Empty : string.Join(',', value);
    }

    [ObservableProperty]
    private string? _decorationBadge;

    [ObservableProperty]
    private string? _decorationToolTip;

    [ObservableProperty]
    private bool _showModifiedDot;

    [ObservableProperty]
    private bool _isReadOnlyEntry;

    [ObservableProperty]
    private bool _hasErrorDecoration;

    [ObservableProperty]
    private string? _gitStatusGlyph;

    public ObservableCollection<FileSystemEntryNode> Children { get; } = new();

    public static FileSystemEntryNode CreateTruncationNotice(string parentPath)
    {
        var node = new FileSystemEntryNode(parentPath, "… (list truncated)", isDirectory: false, isWorkspaceRoot: false)
        {
            IsTruncationPlaceholder = true,
            IsChildrenLoaded = true,
        };
        return node;
    }
}
