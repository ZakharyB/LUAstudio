namespace LUAstudio.IDE.Explorer;

public enum ExplorerEntryKind
{
    WorkspaceRoot,
    Folder,
    FolderOpen,
    Lua,
    Json,
    Xml,
    Markdown,
    Image,
    Code,
    Config,
    Text,
    UnknownFile,
    Truncation,
}

public static class ExplorerEntryKindHelper
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".svg",
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".py", ".go", ".rs", ".java", ".vb",
    };

    public static ExplorerEntryKind Resolve(string displayName, bool isDirectory, bool isWorkspaceRoot, bool isTruncationPlaceholder, bool isExpanded)
    {
        if (isTruncationPlaceholder)
        {
            return ExplorerEntryKind.Truncation;
        }

        if (isDirectory)
        {
            if (isWorkspaceRoot)
            {
                return ExplorerEntryKind.WorkspaceRoot;
            }

            return isExpanded ? ExplorerEntryKind.FolderOpen : ExplorerEntryKind.Folder;
        }

        var ext = Path.GetExtension(displayName);
        return ext switch
        {
            ".lua" => ExplorerEntryKind.Lua,
            ".json" => ExplorerEntryKind.Json,
            ".xml" or ".xaml" => ExplorerEntryKind.Xml,
            ".md" => ExplorerEntryKind.Markdown,
            _ when ImageExtensions.Contains(ext) => ExplorerEntryKind.Image,
            _ when CodeExtensions.Contains(ext) => ExplorerEntryKind.Code,
            ".config" or ".ini" or ".toml" or ".yaml" or ".yml" => ExplorerEntryKind.Config,
            ".txt" or ".log" => ExplorerEntryKind.Text,
            _ => ExplorerEntryKind.UnknownFile,
        };
    }
}
