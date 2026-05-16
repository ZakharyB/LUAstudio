namespace LUAstudio.Languages.Text;

/// <summary>
/// Immutable source buffer with O(1) line/column mapping via precomputed line starts.
/// </summary>
public sealed class SourceText
{
    private readonly int[] _lineStarts;

    private SourceText(string text, int[] lineStarts)
    {
        Text = text;
        Length = text.Length;
        _lineStarts = lineStarts;
    }

    public string Text { get; }

    public int Length { get; }

    public static SourceText From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return new SourceText(text, starts.ToArray());
    }

    public TextPosition GetPosition(int offset)
    {
        offset = Math.Clamp(offset, 0, Length);
        var line = Array.BinarySearch(_lineStarts, offset);
        if (line < 0)
        {
            line = ~line - 1;
        }

        var column = offset - _lineStarts[line];
        return new TextPosition(line, column);
    }

    public int GetOffset(TextPosition position)
    {
        if (position.Line < 0 || position.Line >= _lineStarts.Length)
        {
            return Length;
        }

        return Math.Min(Length, _lineStarts[position.Line] + position.Column);
    }

    public string GetLineText(int line)
    {
        if (line < 0 || line >= _lineStarts.Length)
        {
            return string.Empty;
        }

        var start = _lineStarts[line];
        var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] : Length;
        if (end > start && Text[end - 1] == '\n')
        {
            end--;
        }

        return Text[start..end];
    }
}
