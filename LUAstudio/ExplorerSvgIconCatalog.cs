using System.IO;
using LUAstudio.IDE.Explorer;
using LUAstudio.Workspace;

namespace LUAstudio;

public static class ExplorerSvgIconCatalog
{
    public static string? TryResolveAsset(FileSystemEntryNode node)
    {
        if (node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return null;
        }

        var path = node.FullPath ?? string.Empty;
        var name = node.DisplayName;
        var ext = Path.GetExtension(name);

        if (path.Contains(".github/workflows", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".github\\workflows", StringComparison.OrdinalIgnoreCase))
        {
            return "GitHub Actions.svg";
        }

        if (name.Equals("nuget.config", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            return "NuGet.svg";
        }

        if (name.StartsWith(".git", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase))
        {
            return "Git.svg";
        }

        if (name.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            return "GitHub.svg";
        }

        return ext.ToLowerInvariant() switch
        {
            ".lua" => "Lua.svg",
            ".luau" => "Luau.svg",
            ".json" => "JSON.svg",
            ".xml" or ".xaml" or ".csproj" or ".props" or ".targets" => "XML.svg",
            ".yaml" or ".yml" => "YAML.svg",
            ".ts" or ".tsx" or ".mts" or ".cts" => "TypeScript.svg",
            ".py" or ".pyw" or ".pyi" => "Python.svg",
            ".rs" => "Rust.svg",
            ".ps1" or ".psm1" or ".psd1" or ".ps1xml" => "Powershell.svg",
            ".sql" or ".pgsql" => "PostgresSQL.svg",
            _ => null,
        };
    }

    public static string ResolveGlyph(
        FileSystemEntryNode node,
        bool isExpanded,
        ExplorerEntryKind kind) =>
        kind switch
        {
            ExplorerEntryKind.WorkspaceRoot => ExplorerGlyphs.WorkspaceRoot,
            ExplorerEntryKind.Folder => ExplorerGlyphs.Folder,
            ExplorerEntryKind.FolderOpen => ExplorerGlyphs.FolderOpen,
            ExplorerEntryKind.Truncation => ExplorerGlyphs.Truncation,
            _ => ExplorerGlyphs.UnknownFile,
        };
}
