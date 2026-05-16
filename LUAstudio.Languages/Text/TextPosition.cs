namespace LUAstudio.Languages.Text;

public readonly record struct TextPosition(int Line, int Column)
{
    public static readonly TextPosition Zero = new(0, 0);
}
