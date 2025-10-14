using System.Text.RegularExpressions;

namespace F500.JobMatch.Api.Services.Match;

public class TextClean
{
    private static readonly Regex HtmlRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex NonAlphaRegex = new("[^a-z0-9 ]", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "the",
        "and",
        "or",
        "but",
        "if",
        "is",
        "are",
        "was",
        "were",
        "be",
        "being",
        "been",
        "to",
        "of",
        "in",
        "that",
        "it",
        "for",
        "on",
        "with",
        "as",
        "at",
        "this",
        "by",
        "from",
        "we",
        "you",
        "your",
        "our",
        "they",
        "their",
        "them",
        "he",
        "she",
        "his",
        "her",
        "not",
        "can",
        "will",
        "would",
        "should",
        "could"
    };

    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input = HtmlRegex.Replace(input, " ");
        input = input.ToLowerInvariant();
        input = NonAlphaRegex.Replace(input, " ");
        input = Regex.Replace(input, @"\s+", " ", RegexOptions.Compiled).Trim();
        return input;
    }

    public IReadOnlyList<string> Tokenize(string input)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        foreach (var token in normalized.Split(' '))
        {
            if (token.Length <= 2 || StopWords.Contains(token))
            {
                continue;
            }
            tokens.Add(Stem(token));
        }
        return tokens;
    }

    private static string Stem(string word)
    {
        if (word.EndsWith("ing", StringComparison.Ordinal))
        {
            return word[..^3];
        }
        if (word.EndsWith("ed", StringComparison.Ordinal))
        {
            return word[..^2];
        }
        if (word.EndsWith("es", StringComparison.Ordinal))
        {
            return word[..^2];
        }
        if (word.EndsWith("s", StringComparison.Ordinal) && word.Length > 3)
        {
            return word[..^1];
        }
        return word;
    }
}
