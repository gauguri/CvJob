using F500.JobMatch.Api.Services.Crawl;
using Xunit;

namespace F500.JobMatch.Tests;

public class DetectorTests
{
    [Theory]
    [InlineData("<html><script src='workday.js'></script></html>", AtsType.Workday)]
    [InlineData("<html><script src='greenhouse.js'></script></html>", AtsType.Greenhouse)]
    [InlineData("<html><a href='https://jobs.lever.co'></a></html>", AtsType.Lever)]
    [InlineData("<html><script src='smartrecruiters.js'></script></html>", AtsType.SmartRecruiters)]
    [InlineData("<html><script src='successfactors.js'></script></html>", AtsType.SuccessFactors)]
    [InlineData("<html><script src='taleo.js'></script></html>", AtsType.Taleo)]
    [InlineData("<html><script src='icims.js'></script></html>", AtsType.Icims)]
    public void DetectFromHtml_ReturnsExpectedType(string html, AtsType expected)
    {
        var detectors = new Detectors();
        var result = detectors.DetectFromHtml(html);
        Assert.Equal(expected, result);
    }
}
