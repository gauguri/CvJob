using AngleSharp.Html.Parser;
using F500.JobMatch.Api.Data;

namespace F500.JobMatch.Api.Services.Crawl;

public class Normalizer
{
    private readonly HtmlParser _parser = new();

    public JobPosting Normalize(string company, RawJobPosting raw)
    {
        var text = raw.DescriptionText;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(raw.DescriptionHtml))
        {
            text = ExtractText(raw.DescriptionHtml);
        }

        var stableIdHash = DedupeService.ComputeStableHash(company, raw.Title, raw.Url);
        return new JobPosting
        {
            Id = Guid.NewGuid(),
            StableIdHash = stableIdHash,
            Title = raw.Title,
            Company = company,
            Location = raw.Location,
            DescriptionHtml = raw.DescriptionHtml,
            DescriptionText = text,
            EmploymentType = raw.EmploymentType,
            PostedAtUtc = raw.PostedAtUtc,
            Url = raw.Url,
            Source = raw.Source,
            FetchedAtUtc = DateTime.UtcNow
        };
    }

    private string ExtractText(string html)
    {
        try
        {
            var document = _parser.ParseDocument(html);
            return document.Body?.TextContent?.Trim() ?? string.Empty;
        }
        catch
        {
            return html;
        }
    }
}

public record RawJobPosting
{
    public string Title { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string? EmploymentType { get; init; }
    public string? DescriptionHtml { get; init; }
    public string DescriptionText { get; init; } = string.Empty;
    public DateTime? PostedAtUtc { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}
