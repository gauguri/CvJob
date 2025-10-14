namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class IcimsAdapter : BaseAdapter
{
    public IcimsAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<IcimsAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "iCIMS";

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
            foreach (var card in document.QuerySelectorAll("div.iCIMS_Opportunity"))
            {
                var titleLink = card.QuerySelector("a");
                if (titleLink == null)
                {
                    continue;
                }
                var title = titleLink.TextContent?.Trim() ?? string.Empty;
                if (!IsProductRole(title))
                {
                    continue;
                }
                var href = titleLink.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(careersUri, href, out var jobUri))
                {
                    continue;
                }
                var location = card.QuerySelector("div.iCIMS_JobLocation")?.TextContent?.Trim() ?? string.Empty;
                var detailResponse = await client.GetAsync(jobUri, cancellationToken);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    continue;
                }
                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                var detailDoc = Parser.ParseDocument(detailHtml);
                var descriptionNode = detailDoc.QuerySelector("div.iCIMS_JobContent, #job-content") ?? detailDoc.Body;
                var descriptionHtml = descriptionNode?.InnerHtml ?? detailHtml;
                var descriptionText = descriptionNode?.TextContent ?? detailDoc.Body?.TextContent ?? string.Empty;

                results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, descriptionHtml, descriptionText, null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl iCIMS site {Url}", careersUri);
        }

        return results;
    }
}
