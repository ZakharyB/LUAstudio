using System.Globalization;

namespace LUAstudio.Core;

public static class SettingColorParser
{
    public static uint ParseRgb(string? value, uint defaultRgb)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultRgb;
        }

        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length is 6 or 8 &&
            uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return text.Length == 8 ? parsed & 0x00FFFFFF : parsed;
        }

        return defaultRgb;
    }

    public static string ToHex(uint rgb) =>
        $"#{(rgb & 0xFFFFFF):X6}";
}
