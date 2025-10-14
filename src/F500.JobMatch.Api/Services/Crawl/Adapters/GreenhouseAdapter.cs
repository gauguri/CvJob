namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class GreenhouseAdapter : BaseAdapter
{
    public GreenhouseAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GreenhouseAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "Greenhouse";

    public override async Task<IReadOnlyList<RawJobPosting>> CrawlAsync(string company, Uri careersUri, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("crawler");
        var results = new List<RawJobPosting>();
        try
        {
            var html = await client.GetStringAsync(careersUri, cancellationToken);
            results.AddRange(ParseStructuredData(html, company, careersUri));
            if (results.Count > 0)
            {
                return results;
            }

            var document = Parser.ParseDocument(html);
            foreach (var link in document.QuerySelectorAll("a.opening"))
            {
                var title = link.TextContent?.Trim() ?? string.Empty;
                if (!IsProductRole(title))
                {
                    continue;
                }
                var href = link.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(careersUri, href, out var jobUri))
                {
                    continue;
                }

                var detailResponse = await client.GetAsync(jobUri, cancellationToken);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    continue;
                }
                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                var detailDoc = Parser.ParseDocument(detailHtml);
                var location = detailDoc.QuerySelector(".location")?.TextContent?.Trim() ?? string.Empty;
                var descriptionNode = detailDoc.QuerySelector(".opening, .description, #content") ?? detailDoc.Body;
                var descriptionHtml = descriptionNode?.InnerHtml ?? detailHtml;
                var descriptionText = descriptionNode?.TextContent ?? detailDoc.Body?.TextContent ?? string.Empty;

                results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, descriptionHtml, descriptionText, null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl Greenhouse site {Url}", careersUri);
        }

        return results;
    }
}
