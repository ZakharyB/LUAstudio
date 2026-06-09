using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

/// <summary>
/// Debounced, cancellable parser service. Uses full reparse today; incremental reuse when edit span is small.
/// </summary>
public sealed class LuaParserService : ILuaParser
{
    private const int IncrementalReuseMaxLength = 64;

    public Task<ParseResult> ParseDocumentAsync(
        SourceSnapshot snapshot,
        IncrementalParseContext? previous = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fromCache = false;

            if (previous is not null &&
                previous.ChangedSpan.Length <= IncrementalReuseMaxLength &&
                !previous.PreviousResult.Tree.HasErrors &&
                string.Equals(
                    previous.PreviousResult.Snapshot.Content,
                    snapshot.Content,
                    StringComparison.Ordinal))
            {
                fromCache = true;

                return new ParseResult(
                    previous.PreviousResult.Tree,
                    fromCache,
                    previous.PreviousResult.Tokens);
            }

            var lexer = new LuaLexer(snapshot.Content);
            var allTokens = lexer.Tokenize();

            var (root, diagnostics) = LuaParser.Parse(snapshot.Content);
            var tree = new SyntaxTree(snapshot, root, diagnostics, allTokens);

            return new ParseResult(tree, fromCache, allTokens);

        }, cancellationToken);
    }
}