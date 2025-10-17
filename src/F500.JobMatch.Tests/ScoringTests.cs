using Xunit;
using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Services.Match;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace F500.JobMatch.Tests;

public class ScoringTests
{
    [Fact]
    public async Task ScoreTopAsync_ReturnsRankedResults()
    {
        var options = new DbContextOptionsBuilder<JobMatchDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new JobMatchDbContext(options);
        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            FileName = "resume.txt",
            Text = "Senior product manager with 8 years leading roadmap, experimentation, and AI platforms.",
            CreatedUtc = DateTime.UtcNow
        };
        context.Resumes.Add(resume);
        context.JobPostings.Add(new JobPosting
        {
            Id = Guid.NewGuid(),
            StableIdHash = Guid.NewGuid().ToString("N"),
            Title = "Senior Product Manager",
            Company = "ExampleCorp",
            Location = "Remote",
            DescriptionText = "Looking for a product manager to drive roadmap, experimentation and AI initiatives. 7 years experience preferred.",
            Url = "https://example.com/job",
            Source = "Test",
            FetchedAtUtc = DateTime.UtcNow
        });
        context.JobPostings.Add(new JobPosting
        {
            Id = Guid.NewGuid(),
            StableIdHash = Guid.NewGuid().ToString("N"),
            Title = "Junior Analyst",
            Company = "ExampleCorp",
            Location = "Onsite",
            DescriptionText = "Entry role",
            Url = "https://example.com/job2",
            Source = "Test",
            FetchedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:PreferredLocations:0"] = "Remote"
            })
            .Build();

        var clean = new TextClean();
        var vectorizer = new TfIdfVectorizer(clean);
        var scoring = new MatchScoring(context, vectorizer, clean, configuration);
        var results = await scoring.ScoreTopAsync(resume.Id, 10, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Score >= results[1].Score);
        Assert.InRange(results[0].Score, 0, 100);
    }

    [Fact]
    public async Task ScoreTopAsync_DefaultsToTenWhenTopIsNotPositive()
    {
        var options = new DbContextOptionsBuilder<JobMatchDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new JobMatchDbContext(options);
        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            FileName = "resume.txt",
            Text = "Seasoned product manager with roadmap ownership.",
            CreatedUtc = DateTime.UtcNow
        };
        context.Resumes.Add(resume);
        context.JobPostings.Add(new JobPosting
        {
            Id = Guid.NewGuid(),
            StableIdHash = Guid.NewGuid().ToString("N"),
            Title = "Product Manager",
            Company = "ExampleCorp",
            Location = "Remote",
            DescriptionText = "Product manager role owning the roadmap.",
            Url = "https://example.com/job",
            Source = "Test",
            FetchedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().Build();
        var clean = new TextClean();
        var vectorizer = new TfIdfVectorizer(clean);
        var scoring = new MatchScoring(context, vectorizer, clean, configuration);

        var zeroTopResults = await scoring.ScoreTopAsync(resume.Id, 0, CancellationToken.None);
        var negativeTopResults = await scoring.ScoreTopAsync(resume.Id, -5, CancellationToken.None);

        Assert.Equal(1, zeroTopResults.Count);
        Assert.Equal(1, negativeTopResults.Count);
    }
}
