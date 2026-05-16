using System.IO;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace LUAstudio;

public static class ExplorerSvgIconLoader
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly WpfDrawingSettings DrawingSettings = new() { IncludeRuntime = true };

    private static string IconsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Icons");

    public static ImageSource? TryLoad(string assetFileName)
    {
        if (Cache.TryGetValue(assetFileName, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(IconsDirectory, assetFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var reader = new FileSvgReader(DrawingSettings);
            var drawing = reader.Read(path);
            if (drawing is null)
            {
                return null;
            }

            var image = new DrawingImage(drawing);
            image.Freeze();
            Cache[assetFileName] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }
}
