namespace LUAstudio.Workspace;

public static class WorkspacePathUtilities
{
    public static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        return Path.TrimEndingDirectorySeparator(full);
    }

    public static bool IsPathUnderRoot(string rootPath, string candidatePath)
    {
        var root = NormalizeDirectory(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        if (candidate.Length < root.Length)
        {
            return false;
        }

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (candidate.Length == root.Length)
        {
            return true;
        }

        var sep = candidate[root.Length];
        return sep is '\\' or '/';
    }
}
