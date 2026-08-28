using LUAstudio.IDE.Documents;
using LUAstudio.Workspace;

namespace LUAstudio.IDE.Services;

public sealed class ExplorerNodeDecorationProvider : IExplorerNodeDecorationProvider
{
    private readonly IDocumentService _documents;
    private readonly IGitDecorationProvider _git;

    public ExplorerNodeDecorationProvider(IDocumentService documents, IGitDecorationProvider git)
    {
        _documents = documents;
        _git = git;
    }

    public ExplorerNodeDecoration GetDecoration(FileSystemEntryNode node)
    {
        if (node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return ExplorerNodeDecoration.Empty;
        }

        var badgeParts = new List<string>();
        string? tooltip = null;
        var showModifiedDot = false;
        var hasError = false;
        var gitGlyph = _git.GetGlyph(node.FullPath);

        var doc = _documents.Documents.FirstOrDefault(d =>
            d.FilePath is not null &&
            string.Equals(d.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase));

        if (doc?.IsDirty == true)
        {
            showModifiedDot = true;
            tooltip = "Modified";
        }

        var isReadOnly = false;
        try
        {
            if (File.Exists(node.FullPath))
            {
                isReadOnly = File.GetAttributes(node.FullPath).HasFlag(FileAttributes.ReadOnly);
                if (isReadOnly)
                {
                    badgeParts.Add("RO");
                    tooltip = string.IsNullOrEmpty(tooltip) ? "Read-only" : $"{tooltip}; Read-only";
                }
            }
        }
        catch
        {
            // ignore IO errors for decoration
        }

        var gitToolTip = _git.GetToolTip(node.FullPath);
        if (!string.IsNullOrEmpty(gitToolTip))
        {
            tooltip = string.IsNullOrEmpty(tooltip) ? gitToolTip : $"{tooltip}; {gitToolTip}";
        }

        return new ExplorerNodeDecoration(
            badgeParts.Count > 0 ? string.Join(' ', badgeParts) : null,
            tooltip,
            showModifiedDot,
            isReadOnly,
            hasError,
            gitGlyph);
    }

    public void RefreshAll(IEnumerable<FileSystemEntryNode> roots)
    {
        foreach (var root in roots)
        {
            RefreshNodeRecursive(root);
        }
    }

    public void RefreshPath(string? fullPath, IEnumerable<FileSystemEntryNode> roots)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        foreach (var root in roots)
        {
            var node = FindNodeRecursive(root, fullPath);
            if (node is not null)
            {
                ApplyDecoration(node);
                return;
            }
        }
    }

    private void RefreshNodeRecursive(FileSystemEntryNode node)
    {
        ApplyDecoration(node);
        foreach (var child in node.Children)
        {
            RefreshNodeRecursive(child);
        }
    }

    private void ApplyDecoration(FileSystemEntryNode node)
    {
        var decoration = GetDecoration(node);
        node.DecorationBadge = decoration.Badge;
        node.DecorationToolTip = decoration.BadgeToolTip;
        node.ShowModifiedDot = decoration.ShowModifiedDot;
        node.IsReadOnlyEntry = decoration.IsReadOnly;
        node.HasErrorDecoration = decoration.HasErrorUnderline;
        node.GitStatusGlyph = decoration.GitStatusGlyph;
    }

    private static FileSystemEntryNode? FindNodeRecursive(FileSystemEntryNode node, string fullPath)
    {
        if (string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        if (!node.IsChildrenLoaded)
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeRecursive(child, fullPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
