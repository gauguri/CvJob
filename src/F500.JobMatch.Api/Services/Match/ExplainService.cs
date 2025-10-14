using System.Linq;
using F500.JobMatch.Api.Data;

namespace F500.JobMatch.Api.Services.Match;

public class ExplainService
{

    public IReadOnlyList<string> BuildExplanation(MatchScoreResult score, Resume resume)
    {
        var bullets = new List<string>();

        var keywordSummary = score.KeywordHits
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Take(5)
            .Select(k => k.ToLowerInvariant())
            .ToList();
        if (keywordSummary.Count > 0)
        {
            bullets.Add($"Shared focus on {string.Join(", ", keywordSummary)}.");
        }

        if (score.TitleBoost > 0)
        {
            bullets.Add($"Title closely matches resume focus ({score.Posting.Title}).");
        }

        if (score.LocationBoost > 0)
        {
            var location = score.Posting.Location ?? "Location flexibility";
            bullets.Add($"Location preference aligned ({location}).");
        }

        if (score.ExperienceBoost > 0)
        {
            bullets.Add("Experience level aligns within ±2 years.");
        }

        if (bullets.Count < 3)
        {
            bullets.Add($"High content similarity (TF-IDF base {score.BaseScore:F1}).");
        }

        return bullets.Take(8).ToList();
    }
}
