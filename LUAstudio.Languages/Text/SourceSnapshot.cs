namespace LUAstudio.Languages.Text;

/// <summary>
/// Versioned immutable view of document text for parse/analysis pipelines.
/// </summary>
public sealed class SourceSnapshot
{
    public SourceSnapshot(Guid documentId, int version, SourceText text, string? filePath, LuaDialect dialect)
    {
        DocumentId = documentId;
        Version = version;
        Text = text;
        FilePath = filePath;
        Dialect = dialect;
    }

    public Guid DocumentId { get; }

    public int Version { get; }

    public SourceText Text { get; }

    public string? FilePath { get; }

    public LuaDialect Dialect { get; }

    public string Content => Text.Text;

    public SourceSnapshot WithText(int version, string content) =>
        new(DocumentId, version, SourceText.From(content), FilePath, Dialect);
}

public enum LuaDialect
{
    Lua,
    Luau
}
