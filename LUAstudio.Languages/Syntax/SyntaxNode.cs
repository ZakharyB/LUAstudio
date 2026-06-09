using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Syntax;

/// <summary>
/// Immutable syntax node (red tree). Parent links are weak for traversal; children are owned.
/// </summary>
public abstract class SyntaxNode
{
    protected SyntaxNode(SyntaxKind kind, TextSpan span, SyntaxNode? parent)
    {
        Kind = kind;
        Span = span;
        Parent = parent;
    }

    public SyntaxKind Kind { get; }

    public TextSpan Span { get; }

    public SyntaxNode? Parent { get; }

    public abstract IReadOnlyList<SyntaxNode> Children { get; }

    public IEnumerable<SyntaxNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var node in child.DescendantsAndSelf())
            {
                yield return node;
            }
        }
    }

    public SyntaxNode? FindNodeAt(int offset)
    {
        if (!Span.Contains(offset) && offset != Span.End)
        {
            return null;
        }

        foreach (var child in Children)
        {
            var found = child.FindNodeAt(offset);
            if (found is not null)
            {
                return found;
            }
        }

        return this;
    }
}
