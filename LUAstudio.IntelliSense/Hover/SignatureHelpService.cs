using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense;

public sealed record SignatureInfo(string Label, string? Documentation, int ParameterIndex);

public sealed class SignatureHelpService
{
    private readonly IRobloxApiDatabase _roblox;
    private readonly ExpressionTypeResolver _types;

    public SignatureHelpService(IRobloxApiDatabase roblox, ExpressionTypeResolver types)
    {
        _roblox = roblox;
        _types = types;
    }

    public async Task<SignatureInfo?> GetSignatureAsync(
        Completion.CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var call = context.NodeAtCaret;
        while (call is not null && call is not CallExpressionSyntax)
            call = call.Parent;
        if (call is not CallExpressionSyntax callExpression)
        {
            return null;
        }

        if (callExpression.Target is MemberAccessExpressionSyntax member &&
            _types.ResolveType(member.Expression, context.SemanticModel?.RootScope) is { } calleeType &&
            _roblox.TryGetMember(calleeType, member.Member.Text, out var m))
        {
            var paramIndex = CountCommas(context.Snapshot.Content, callExpression.Span.Start, context.CaretOffset);
            return new SignatureInfo(
                $"{member.Member.Text}({string.Join(", ", m.Parameters ?? [])})",
                m.Documentation,
                paramIndex);
        }

        return null;
    }

    private static int CountCommas(string text, int callStart, int offset)
    {
        var depth = 0;
        var commas = 0;
        for (var i = Math.Max(0, callStart); i < offset && i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(': depth++; break;
                case ')': depth--; break;
                case ',' when depth == 1: commas++; break;
            }
        }

        return commas;
    }
}
