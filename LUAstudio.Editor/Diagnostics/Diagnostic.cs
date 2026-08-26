namespace LUAstudio.Editor.Diagnostics
{
    public class Diagnostic
    {
        public int Offset { get; }
        public int Length { get; }

        public Diagnostic(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }
    }
}