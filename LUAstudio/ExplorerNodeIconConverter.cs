using System.Globalization;
using System.Windows.Data;
using LUAstudio.IDE.Explorer;
using LUAstudio.Workspace;

namespace LUAstudio;

public sealed class ExplorerNodeIconConverter : IMultiValueConverter
{
    public static ExplorerNodeIconConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 1 || values[0] is not FileSystemEntryNode node)
        {
            return new ExplorerIconDisplay { Glyph = ExplorerGlyphs.UnknownFile };
        }

        var isExpanded = values.Length > 1 && values[1] is true;
        var kind = ExplorerEntryKindHelper.Resolve(
            node.DisplayName,
            node.IsDirectory,
            node.IsWorkspaceRoot,
            node.IsTruncationPlaceholder,
            isExpanded);

        var glyph = ExplorerSvgIconCatalog.ResolveGlyph(node, isExpanded, kind);
        var asset = ExplorerSvgIconCatalog.TryResolveAsset(node);
        if (asset is not null)
        {
            var image = ExplorerSvgIconLoader.TryLoad(asset);
            if (image is not null)
            {
                return new ExplorerIconDisplay { Image = image, Glyph = glyph };
            }
        }

        return new ExplorerIconDisplay { Glyph = glyph };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
