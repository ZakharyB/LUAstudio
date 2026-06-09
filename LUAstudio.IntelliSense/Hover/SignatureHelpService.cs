using LUAstudio.IntelliSense.Roblox;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense;

public sealed record SignatureInfo(string Label, string? Documentation, int ParameterIndex);

public sealed class SignatureHelpService
{
    private readonly IRobloxApiDatabase _roblox;

    public SignatureHelpService(IRobloxApiDatabase roblox) => _roblox = roblox;

    public async Task<SignatureInfo?> GetSignatureAsync(
        Completion.CompletionContext context,
        CancellationToken cancellationToken = default)
    {
        await _roblox.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (context.NodeAtCaret is not CallExpressionSyntax call)
        {
            return null;
        }

        if (call.Target is MemberAccessExpressionSyntax member &&
            _roblox.TryGetMember("Instance", member.Member.Text, out var m))
        {
            var paramIndex = CountCommas(context.Snapshot.Content, context.CaretOffset);
            return new SignatureInfo(
                $"{member.Member.Text}({string.Join(", ", m.Parameters ?? [])})",
                m.Documentation,
                paramIndex);
        }

        return null;
    }

    private static int CountCommas(string text, int offset)
    {
        var depth = 0;
        var commas = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
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
