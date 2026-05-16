namespace LUAstudio.IDE.Explorer;

/// <summary>Case-insensitive subsequence fuzzy match for explorer filter text.</summary>
public static class FuzzyExplorerMatcher
{
    public static bool TryMatch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, out int[] matchIndices)
    {
        matchIndices = Array.Empty<int>();
        if (pattern.IsEmpty)
        {
            return true;
        }

        if (text.IsEmpty)
        {
            return false;
        }

        var indices = new List<int>(pattern.Length);
        var patternIndex = 0;
        for (var i = 0; i < text.Length && patternIndex < pattern.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) == char.ToLowerInvariant(pattern[patternIndex]))
            {
                indices.Add(i);
                patternIndex++;
            }
        }

        if (patternIndex < pattern.Length)
        {
            return false;
        }

        matchIndices = indices.ToArray();
        return true;
    }

    public static int Score(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, int[] matchIndices)
    {
        if (matchIndices.Length == 0)
        {
            return pattern.IsEmpty ? 0 : int.MinValue;
        }

        var score = 0;
        var consecutive = 0;
        var previous = -2;
        for (var i = 0; i < matchIndices.Length; i++)
        {
            var index = matchIndices[i];
            if (index == previous + 1)
            {
                consecutive++;
                score -= consecutive * 4;
            }
            else
            {
                consecutive = 0;
            }

            if (index == 0)
            {
                score -= 10;
            }
            else if (index > 0 && (text[index - 1] == '_' || text[index - 1] == '-' || text[index - 1] == '.' || text[index - 1] == ' '))
            {
                score -= 6;
            }

            if (char.IsUpper(text[index]))
            {
                score -= 2;
            }

            previous = index;
        }

        score -= (text.Length - pattern.Length) * 2;
        return score;
    }
}
