using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Syntax;

public sealed class SyntaxTree
{
    public SyntaxTree(
        SourceSnapshot snapshot,
        SyntaxNode root,
        IReadOnlyList<ParseDiagnostic> diagnostics,
        IReadOnlyList<LuaToken> tokens)
    {
        Snapshot = snapshot;
        Root = root;
        Diagnostics = diagnostics;
        Tokens = tokens;
    }

    public SourceSnapshot Snapshot { get; }

    public SyntaxNode Root { get; }

    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

    public IReadOnlyList<LuaToken> Tokens { get; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}