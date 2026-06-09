using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

public interface ILuaParser
{
    Task<ParseResult> ParseDocumentAsync(
        SourceSnapshot snapshot,
        IncrementalParseContext? previous = null,
        CancellationToken cancellationToken = default);
}

public sealed class IncrementalParseContext
{
    public IncrementalParseContext(ParseResult previousResult, TextSpan changedSpan)
    {
        PreviousResult = previousResult;
        ChangedSpan = changedSpan;
    }

    public ParseResult PreviousResult { get; }

    public TextSpan ChangedSpan { get; }
}
