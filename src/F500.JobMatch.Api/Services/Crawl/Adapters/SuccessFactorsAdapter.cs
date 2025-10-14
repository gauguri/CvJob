namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class SuccessFactorsAdapter : BaseAdapter
{
    public SuccessFactorsAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SuccessFactorsAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "SuccessFactors";

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
            foreach (var row in document.QuerySelectorAll("tr.data-row"))
            {
                var titleLink = row.QuerySelector("a.jobTitle-link");
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
                var location = row.QuerySelector("td[data-th='Location']")?.TextContent?.Trim() ?? string.Empty;
                var detailResponse = await client.GetAsync(jobUri, cancellationToken);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    continue;
                }
                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                var detailDoc = Parser.ParseDocument(detailHtml);
                var descriptionNode = detailDoc.QuerySelector("div.job-description, #job-summary") ?? detailDoc.Body;
                var descriptionHtml = descriptionNode?.InnerHtml ?? detailHtml;
                var descriptionText = descriptionNode?.TextContent ?? detailDoc.Body?.TextContent ?? string.Empty;

                results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, descriptionHtml, descriptionText, null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl SuccessFactors site {Url}", careersUri);
        }

        return results;
    }
}
