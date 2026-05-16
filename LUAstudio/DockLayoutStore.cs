using System.IO;

namespace LUAstudio;

/// <summary>Legacy AvalonDock layout file helper. Layout now uses a fixed Grid; this only cleans up old saves.</summary>
public sealed class DockLayoutStore
{
    private static string LayoutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LuaStudio",
            "dock-layout.xml");

    public static void DeleteLegacyLayoutFile()
    {
        try
        {
            if (File.Exists(LayoutPath))
            {
                File.Delete(LayoutPath);
            }
        }
        catch
        {
            // Non-fatal.
        }
    }
}
