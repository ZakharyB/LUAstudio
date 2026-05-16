using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace LUAstudio;

internal static class DependencyObjectExtensions
{
    public static DependencyObject? GetParentObject(this DependencyObject child) =>
        child switch
        {
            Visual or Visual3D => VisualTreeHelper.GetParent(child),
            FrameworkContentElement contentElement => contentElement.Parent,
            _ => null
        };

    public static T? FindAncestor<T>(this DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = child.GetParentObject();
        }

        return null;
    }
}
