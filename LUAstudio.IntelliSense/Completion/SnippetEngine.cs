using System.Text;
using System.Text.RegularExpressions;

namespace LUAstudio.IntelliSense.Completion;

public sealed record SnippetPlaceholder(int Index, string? Label, int Start, int Length);

public sealed record SnippetExpansion(string Text, IReadOnlyList<SnippetPlaceholder> Placeholders);

public static class SnippetEngine
{
    private static readonly Regex PlaceholderRegex = new(
        @"\$\{(\d+)(?::([^}]*))?\}",
        RegexOptions.Compiled);

    public static bool ContainsPlaceholders(string text) => text.Contains("${", StringComparison.Ordinal);

    public static SnippetExpansion Expand(string template)
    {
        var placeholders = new List<SnippetPlaceholder>();
        var built = new StringBuilder();
        var last = 0;

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            built.Append(template, last, match.Index - last);
            var index = int.Parse(match.Groups[1].Value);
            var label = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            var start = built.Length;
            built.Append(label);
            placeholders.Add(new SnippetPlaceholder(index, match.Groups[2].Success ? match.Groups[2].Value : null, start, label.Length));
            last = match.Index + match.Length;
        }

        built.Append(template, last, template.Length - last);
        return new SnippetExpansion(built.ToString(), placeholders.OrderBy(p => p.Index).ToArray());
    }

    public static string GetDisplayText(string insertText) =>
        ContainsPlaceholders(insertText)
            ? PlaceholderRegex.Replace(insertText, m => m.Groups[2].Success ? m.Groups[2].Value : string.Empty)
            : insertText;
}
