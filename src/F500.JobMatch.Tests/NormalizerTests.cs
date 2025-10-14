using F500.JobMatch.Api.Services.Crawl;
using Xunit;

namespace F500.JobMatch.Tests;

public class NormalizerTests
{
    [Fact]
    public void Normalize_GeneratesStableHashAndText()
    {
        var normalizer = new Normalizer();
        var raw = new RawJobPosting
        {
            Title = "Product Manager",
            Location = "Remote",
            DescriptionHtml = "<p>Build product roadmap</p>",
            Url = "https://example.com/job",
            Source = "Test"
        };

        var job = normalizer.Normalize("ExampleCo", raw);
        Assert.Equal("ExampleCo", job.Company);
        Assert.False(string.IsNullOrWhiteSpace(job.DescriptionText));
        Assert.False(string.IsNullOrWhiteSpace(job.StableIdHash));
    }
}
