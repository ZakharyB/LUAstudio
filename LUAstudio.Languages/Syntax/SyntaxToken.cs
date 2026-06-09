using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Syntax;

public sealed class SyntaxToken : SyntaxNode
{
    public SyntaxToken(string text, TextSpan span, SyntaxNode? parent)
        : base(SyntaxKind.IdentifierName, span, parent)
    {
        Text = text;
    }

    public string Text { get; }

    public override IReadOnlyList<SyntaxNode> Children => Array.Empty<SyntaxNode>();
}
