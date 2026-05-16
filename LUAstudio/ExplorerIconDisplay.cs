using System.Windows.Media;

namespace LUAstudio;

public sealed class ExplorerIconDisplay
{
    public ImageSource? Image { get; init; }

    public string Glyph { get; init; } = ExplorerGlyphs.UnknownFile;

    public bool HasImage => Image is not null;
}
