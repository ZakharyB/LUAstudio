using System.Windows.Media;

namespace LUAstudio.Editor.Highlighting;

internal static class HighlightBrushes
{
    public static readonly SolidColorBrush Text = Freeze(0xBC, 0xBE, 0xC8);
    public static readonly SolidColorBrush Operator = Freeze(0xAA, 0xAF, 0xB9);
    public static readonly SolidColorBrush Number = Freeze(0xBE, 0xD7, 0xAA);
    public static readonly SolidColorBrush String = Freeze(0xCE, 0x91, 0x78);
    public static readonly SolidColorBrush Comment = Freeze(0x6A, 0x99, 0x55);
    public static readonly SolidColorBrush Keyword = Freeze(0xC5, 0x86, 0xC0);
    public static readonly SolidColorBrush Bool = Freeze(0x56, 0x9C, 0xD6);
    public static readonly SolidColorBrush Information = Freeze(0x56, 0x9C, 0xD6);
    public static readonly SolidColorBrush Builtin = Freeze(0x4E, 0xC9, 0xB0);
    public static readonly SolidColorBrush LocalMethod = Freeze(0xD4, 0xD4, 0xAA);
    public static readonly SolidColorBrush LocalProperty = Freeze(0x9C, 0xDC, 0xFE);
    public static readonly SolidColorBrush FunctionName = Freeze(0xDC, 0xDC, 0xAA);
    public static readonly SolidColorBrush Type = Freeze(0x4F, 0xC1, 0xFF);
    public static readonly SolidColorBrush Todo = Freeze(0xFF, 0xCC, 0x66);
    public static readonly SolidColorBrush Bracket = Freeze(0xBC, 0xBE, 0xC8);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
