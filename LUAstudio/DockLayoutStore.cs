using System.IO;
using AvalonDock.Layout.Serialization;
using AvalonDock;

namespace LUAstudio;

public sealed class DockLayoutStore
{
    private readonly string _path;

    public DockLayoutStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LuaStudio");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "dock-layout.xml");
    }

    public void Save(DockingManager manager)
    {
        var serializer = new XmlLayoutSerializer(manager);
        serializer.Serialize(_path);
    }

    public void TryLoad(DockingManager manager)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var serializer = new XmlLayoutSerializer(manager);
        serializer.Deserialize(_path);
    }
}
