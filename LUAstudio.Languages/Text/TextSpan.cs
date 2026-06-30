namespace LUAstudio.Languages.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool IsEmpty => Length == 0;

    public bool Contains(int position) =>
        position >= Start && position < End;

    public bool Contains(TextSpan other) =>
        other.Start >= Start && other.End <= End;

    public bool Overlaps(TextSpan other) =>
        Start < other.End && other.Start < End;

    public TextSpan Union(TextSpan other)
    {
        int start = Math.Min(Start, other.Start);
        int end = Math.Max(End, other.End);
        return FromBounds(start, end);
    }

    public TextSpan Translate(int offset) =>
        new(Start + offset, Length);

    public static TextSpan FromBounds(int start, int end) =>
        new(start, end - start);

    public override string ToString() =>
        $"[{Start}..{End})";
}