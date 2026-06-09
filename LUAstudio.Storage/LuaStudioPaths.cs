namespace LUAstudio.Storage;

public static class LuaStudioPaths
{
    public static string AppDataRoot
    {
        get
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LuaStudio");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string DatabasePath => Path.Combine(AppDataRoot, "luastudio.db");

    public static string CacheDirectory
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "cache");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
