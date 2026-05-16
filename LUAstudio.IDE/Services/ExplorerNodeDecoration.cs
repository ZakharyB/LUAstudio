namespace LUAstudio.IDE.Services;

public sealed record ExplorerNodeDecoration(
    string? Badge,
    string? BadgeToolTip,
    bool ShowModifiedDot,
    bool IsReadOnly,
    bool HasErrorUnderline,
    string? GitStatusGlyph)
{
    public static ExplorerNodeDecoration Empty { get; } = new(null, null, false, false, false, null);
}
