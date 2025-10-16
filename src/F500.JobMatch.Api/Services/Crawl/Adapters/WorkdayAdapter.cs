using System.Text.Json;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class WorkdayAdapter : BaseAdapter
{
    public WorkdayAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WorkdayAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "Workday";

    public override async Task<IReadOnlyList<RawJobPosting>> CrawlAsync(string company, Uri careersUri, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("crawler");
        var results = new List<RawJobPosting>();
        try
        {
            var html = await client.GetStringAsync(careersUri, cancellationToken);
            results.AddRange(ParseStructuredData(html, company, careersUri));

            if (results.Count == 0)
            {
                var document = Parser.ParseDocument(html);
                foreach (var card in document.QuerySelectorAll("div[data-automation='job']"))
                {
                    var title = card.QuerySelector("a[data-automation='jobTitle']")?.TextContent?.Trim();
                    var href = card.QuerySelector("a[data-automation='jobTitle']")?.GetAttribute("href");
                    var location = card.QuerySelector("span[data-automation='jobLocation']")?.TextContent?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href))
                    {
                        continue;
                    }
                    if (!IsProductRole(title))
                    {
                        continue;
                    }
                    if (!Uri.TryCreate(careersUri, href, out var jobUri))
                    {
                        continue;
                    }
                    var detail = await client.GetAsync(jobUri, cancellationToken);
                    if (!detail.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    var detailHtml = await detail.Content.ReadAsStringAsync(cancellationToken);
                    var detailDoc = Parser.ParseDocument(detailHtml);
                    var description = detailDoc.QuerySelector("div[data-automation='jobDescription']")?.InnerHtml ?? detailHtml;
                    var descriptionText = detailDoc.QuerySelector("div[data-automation='jobDescription']")?.TextContent ?? detailDoc.Body?.TextContent ?? string.Empty;
                    results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, description, descriptionText, null, null));
                }
            }

            if (results.Count == 0)
            {
                results.AddRange(ParseMosaicData(html, company, careersUri));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl Workday site {Url}", careersUri);
        }

        return results;
    }

    private IEnumerable<RawJobPosting> ParseMosaicData(string html, string company, Uri baseUri)
    {
        const string marker = "jobPostings";
        var index = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Array.Empty<RawJobPosting>();
        }

        var start = html.IndexOf('[', index);
        var end = html.IndexOf(']', start);
        if (start < 0 || end < 0)
        {
            return Array.Empty<RawJobPosting>();
        }

        var json = html.Substring(start, end - start + 1);
        var results = new List<RawJobPosting>();
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() ?? string.Empty : string.Empty;
                if (!IsProductRole(title))
                {
                    continue;
                }
                var location = item.TryGetProperty("secondaryText", out var locationProperty) ? locationProperty.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("externalPath", out var pathProperty) ? pathProperty.GetString() ?? baseUri.AbsoluteUri : baseUri.AbsoluteUri;
                if (Uri.TryCreate(baseUri, url, out var absolute))
                {
                    url = absolute.AbsoluteUri;
                }
                results.Add(CreatePosting(title, url, company, location, null, string.Empty, null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse Workday mosaic data");
        }

        return results;
    }
}
