using LUAstudio.Workspace;

namespace LUAstudio.IDE.Explorer;

public static class ExplorerFilterEngine
{
    public static void ClearFilter(IEnumerable<FileSystemEntryNode> roots)
    {
        foreach (var root in roots)
        {
            ClearNodeRecursive(root);
        }
    }

    public static void ApplyFilter(IEnumerable<FileSystemEntryNode> roots, string? filterText)
    {
        var pattern = filterText?.Trim() ?? string.Empty;
        if (pattern.Length == 0)
        {
            ClearFilter(roots);
            return;
        }

        foreach (var root in roots)
        {
            ApplyToSubtree(root, pattern);
        }
    }

    public static IReadOnlyList<FileSystemEntryNode> CollectExpandPathNodes(IEnumerable<FileSystemEntryNode> roots, string? filterText)
    {
        var pattern = filterText?.Trim() ?? string.Empty;
        if (pattern.Length == 0)
        {
            return Array.Empty<FileSystemEntryNode>();
        }

        var expand = new List<FileSystemEntryNode>();
        foreach (var root in roots)
        {
            CollectExpandRecursive(root, pattern, expand);
        }

        return expand;
    }

    private static bool ApplyToSubtree(FileSystemEntryNode node, string pattern)
    {
        var selfMatches = FuzzyExplorerMatcher.TryMatch(node.DisplayName, pattern, out var indices);
        if (selfMatches)
        {
            node.IsFilterNameMatch = true;
            node.FilterMatchIndices = indices;
        }
        else
        {
            node.IsFilterNameMatch = false;
            node.FilterMatchIndices = Array.Empty<int>();
        }

        var childVisible = false;
        if (node.IsDirectory && !node.IsTruncationPlaceholder)
        {
            foreach (var child in node.Children)
            {
                if (ApplyToSubtree(child, pattern))
                {
                    childVisible = true;
                }
            }
        }

        var visible = selfMatches || childVisible;
        node.IsVisibleInFilter = visible;
        return visible;
    }

    private static void CollectExpandRecursive(FileSystemEntryNode node, string pattern, List<FileSystemEntryNode> expand)
    {
        if (!node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return;
        }

        var selfMatches = FuzzyExplorerMatcher.TryMatch(node.DisplayName, pattern, out _);
        var childNeedsExpand = false;
        foreach (var child in node.Children)
        {
            if (!child.IsVisibleInFilter)
            {
                continue;
            }

            if (child.IsDirectory && !child.IsTruncationPlaceholder)
            {
                CollectExpandRecursive(child, pattern, expand);
                childNeedsExpand = true;
            }
            else if (child.IsFilterNameMatch)
            {
                childNeedsExpand = true;
            }
        }

        if (childNeedsExpand && !selfMatches)
        {
            expand.Add(node);
        }
    }

    private static void ClearNodeRecursive(FileSystemEntryNode node)
    {
        node.IsVisibleInFilter = true;
        node.IsFilterNameMatch = false;
        node.FilterMatchIndices = Array.Empty<int>();

        foreach (var child in node.Children)
        {
            ClearNodeRecursive(child);
        }
    }
}
