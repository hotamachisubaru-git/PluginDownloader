using System.Text.RegularExpressions;

namespace PluginDownloader.Services;

public static class NameMatcher
{
    public static int Score(string targetName, params string?[] candidates)
    {
        var normalizedTarget = Normalize(targetName);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return 0;
        }

        var targetTokens = Tokenize(targetName);
        var bestScore = 0;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalizedCandidate = Normalize(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                continue;
            }

            var score = 0;

            if (string.Equals(normalizedCandidate, normalizedTarget, StringComparison.Ordinal))
            {
                score += 120;
            }

            if (normalizedCandidate.StartsWith(normalizedTarget, StringComparison.Ordinal) ||
                normalizedTarget.StartsWith(normalizedCandidate, StringComparison.Ordinal))
            {
                score += 70;
            }

            if (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal) ||
                normalizedTarget.Contains(normalizedCandidate, StringComparison.Ordinal))
            {
                score += 45;
            }

            var candidateTokens = Tokenize(candidate);
            var overlapCount = targetTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
            score += overlapCount * 12;

            bestScore = Math.Max(bestScore, score);
        }

        return bestScore;
    }

    private static IReadOnlySet<string> Tokenize(string text)
    {
        var matches = Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+");
        return matches.Select(static match => match.Value).ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string input)
    {
        var chars = input
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }
}
