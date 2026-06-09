namespace LUAstudio.Languages.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool Contains(int offset) => offset >= Start && offset < End;

    public bool Overlaps(TextSpan other) =>
        Start < other.End && other.Start < End;

    public static TextSpan FromBounds(int start, int end) => new(start, end - start);
}
