using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

public sealed class ParseResult
{
    public ParseResult(SyntaxTree tree, bool fromIncrementalCache)
    {
        Tree = tree;
        FromIncrementalCache = fromIncrementalCache;
    }

    public SyntaxTree Tree { get; }

    public SourceSnapshot Snapshot => Tree.Snapshot;

    public bool FromIncrementalCache { get; }
}
