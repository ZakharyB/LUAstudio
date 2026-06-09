using System.Windows.Media;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Editor.Highlighting;

public static class HighlightBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static SolidColorBrush Text => Get(SettingKeys.EditorColorText, 0xBCBEC8);
    public static SolidColorBrush Operator => Get(SettingKeys.EditorColorOperator, 0xD4D4D4);
    public static SolidColorBrush Number => Get(SettingKeys.EditorColorNumber, 0xB5CEA8);
    public static SolidColorBrush String => Get(SettingKeys.EditorColorString, 0xCE9178);
    public static SolidColorBrush Comment => Get(SettingKeys.EditorColorComment, 0x6A9955);
    public static SolidColorBrush Keyword => Get(SettingKeys.EditorColorKeyword, 0xC586C8);
    public static SolidColorBrush Bool => Get(SettingKeys.EditorColorGlobal, 0x569CD6);
    public static SolidColorBrush Information => Get(SettingKeys.EditorColorGlobal, 0x569CD6);
    public static SolidColorBrush Builtin => Get(SettingKeys.EditorColorBuiltin, 0x4EC9B0);
    public static SolidColorBrush LocalMethod => Get(SettingKeys.EditorColorLocalMethod, 0xD4D4AA);
    public static SolidColorBrush LocalProperty => Get(SettingKeys.EditorColorLocalProperty, 0x9CDCFE);
    public static SolidColorBrush FunctionName => Get(SettingKeys.EditorColorFunction, 0xDCDCAA);
    public static SolidColorBrush Type => Get(SettingKeys.EditorColorType, 0x4EC9B0);
    public static SolidColorBrush Todo => Get(SettingKeys.EditorColorTodo, 0xFFCC66);
    public static SolidColorBrush Bracket => Get(SettingKeys.EditorColorBracket, 0xBCBEC8);

    public static void Invalidate() => Cache.Clear();

    private static SolidColorBrush Get(string key, uint defaultRgb)
    {
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var hex = Engine.Globals.Get<string>(key)?.Value;
        var rgb = SettingColorParser.ParseRgb(hex, defaultRgb);
        var brush = new SolidColorBrush(ColorFromRgb(rgb));
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }

    private static Color ColorFromRgb(uint rgb) =>
        Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
}
