using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using Microsoft.EntityFrameworkCore;
using F500.JobMatch.Api.Data;

namespace F500.JobMatch.Api.Services.Match;

public class MatchScoring
{
    private const int DefaultTopMatches = 10;
    private static readonly Regex YearsRegex = new("(\\d+)\\+?\\s*years", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleRegex = new("(?i)(product\\s*manager|senior\\s*product\\s*manager|group\\s*pm|principal\\s*pm|director\\s*of\\s*product|product\\s*lead|technical\\s*product\\s*manager|platform\\s*pm|growth\\s*product\\s*manager|ai\\s*product\\s*manager|ml\\s*product\\s*manager|data\\s*product\\s*manager)");
    private static readonly Dictionary<string, double> KeywordWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["product"] = 1.0,
        ["roadmap"] = 1.0,
        ["backlog"] = 0.8,
        ["stakeholders"] = 0.8,
        ["a/b testing"] = 1.2,
        ["experimentation"] = 1.0,
        ["analytics"] = 0.9,
        ["sql"] = 1.0,
        ["tableau"] = 0.8,
        ["python"] = 0.9,
        ["api"] = 0.7,
        ["platform"] = 0.7,
        ["saas"] = 1.1,
        ["b2b"] = 0.9,
        ["fintech"] = 1.0,
        ["ecommerce"] = 0.9,
        ["ai"] = 1.2,
        ["ml"] = 1.2,
        ["llm"] = 1.2,
        ["nlp"] = 1.0,
        ["computer vision"] = 0.8,
        ["payments"] = 0.9,
        ["growth"] = 1.0,
        ["plg"] = 0.8,
        ["okr"] = 0.6,
        ["kpi"] = 0.6,
        ["jira"] = 0.6,
        ["agile"] = 0.7,
        ["scrum"] = 0.7
    };

    private readonly JobMatchDbContext _dbContext;
    private readonly TfIdfVectorizer _vectorizer;
    private readonly TextClean _clean;
    private readonly IConfiguration _configuration;

    public MatchScoring(JobMatchDbContext dbContext, TfIdfVectorizer vectorizer, TextClean clean, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _vectorizer = vectorizer;
        _clean = clean;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<MatchScoreResult>> ScoreTopAsync(Guid resumeId, int top, CancellationToken cancellationToken)
    {
        if (top <= 0)
        {
            top = DefaultTopMatches;
        }

        var resume = await _dbContext.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId, cancellationToken);
        if (resume == null)
        {
            throw new InvalidOperationException($"Resume {resumeId} not found");
        }

        var postings = await _dbContext.JobPostings.AsNoTracking().ToListAsync(cancellationToken);
        if (postings.Count == 0)
        {
            return Array.Empty<MatchScoreResult>();
        }

        var preferredLocations = _configuration.GetSection("Matching:PreferredLocations").Get<string[]>() ?? Array.Empty<string>();

        var resumeTokens = _vectorizer.Tokenize(resume.Text);
        var jobTokensList = postings.Select(p => _vectorizer.Tokenize(p.DescriptionText)).ToList();
        var idf = _vectorizer.ComputeIdf(jobTokensList.Append(resumeTokens));
        var resumeVector = _vectorizer.ComputeTfIdf(resumeTokens, idf);

        var results = new List<MatchScoreResult>();

        foreach (var posting in postings)
        {
            var jobTokens = _vectorizer.Tokenize(posting.DescriptionText);
            var jobVector = _vectorizer.ComputeTfIdf(jobTokens, idf);
            var cosine = _vectorizer.CosineSimilarity(resumeVector, jobVector);
            var baseScore = Math.Clamp(cosine * 100, 0, 100);

            var titleBoost = CalculateTitleBoost(posting.Title, resume.Text);
            var keywordInfo = CalculateKeywordBoost(resume.Text, posting.DescriptionText);
            var locationBoost = CalculateLocationBoost(posting.Location, preferredLocations);
            var experienceBoost = CalculateExperienceBoost(resume.Text, posting.DescriptionText);

            var totalScore = Math.Clamp(baseScore + titleBoost + keywordInfo.Boost + locationBoost + experienceBoost, 0, 100);

            results.Add(new MatchScoreResult(posting, totalScore, baseScore, titleBoost, keywordInfo.Boost, locationBoost, experienceBoost, keywordInfo.Hits, keywordInfo.Miss));
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Posting.Title)
            .Take(top)
            .ToList();
    }

    private double CalculateTitleBoost(string title, string resumeText)
    {
        if (!TitleRegex.IsMatch(title))
        {
            return 0;
        }
        var cleanedTitle = title.ToLowerInvariant();
        var cleanedResume = resumeText.ToLowerInvariant();
        var ratio = Fuzz.TokenSetRatio(cleanedTitle, cleanedResume);
        return Math.Min(10, ratio / 10.0);
    }

    private (double Boost, IReadOnlyList<string> Hits, IReadOnlyList<string> Miss) CalculateKeywordBoost(string resumeText, string jobText)
    {
        var hits = new List<string>();
        var miss = new List<string>();
        double boost = 0;
        var normalizedJob = _clean.Normalize(jobText);
        var normalizedResume = _clean.Normalize(resumeText);
        foreach (var kvp in KeywordWeights)
        {
            if (normalizedJob.Contains(kvp.Key) || normalizedResume.Contains(kvp.Key))
            {
                hits.Add(kvp.Key);
                boost += kvp.Value;
            }
            else
            {
                miss.Add(kvp.Key);
            }
        }
        return (Boost: Math.Min(10, boost), Hits: hits, Miss: miss);
    }

    private double CalculateLocationBoost(string? location, string[] preferredLocations)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return 0;
        }
        if (location.Contains("remote", StringComparison.OrdinalIgnoreCase) || location.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }
        foreach (var preferred in preferredLocations)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && location.Contains(preferred, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }
        }
        return 0;
    }

    private double CalculateExperienceBoost(string resumeText, string jobText)
    {
        var resumeMatch = YearsRegex.Match(resumeText);
        var jobMatch = YearsRegex.Match(jobText);
        if (!resumeMatch.Success || !jobMatch.Success)
        {
            return 0;
        }
        var resumeYears = int.Parse(resumeMatch.Groups[1].Value);
        var jobYears = int.Parse(jobMatch.Groups[1].Value);
        return Math.Abs(resumeYears - jobYears) <= 2 ? 5 : 0;
    }
}

public record MatchScoreResult(
    JobPosting Posting,
    double Score,
    double BaseScore,
    double TitleBoost,
    double KeywordBoost,
    double LocationBoost,
    double ExperienceBoost,
    IReadOnlyList<string> KeywordHits,
    IReadOnlyList<string> KeywordMisses);
