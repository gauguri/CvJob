using AngleSharp.Dom;
namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class TaleoAdapter : BaseAdapter
{
    public TaleoAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TaleoAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "Taleo";

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
            foreach (var link in document.QuerySelectorAll("a.taleosearchlink, a.titlelink"))
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
                var row = link.Closest("tr");
                var location = row?.QuerySelector("td:nth-of-type(3)")?.TextContent?.Trim() ?? string.Empty;
                var detailResponse = await client.GetAsync(jobUri, cancellationToken);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    continue;
                }
                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                var detailDoc = Parser.ParseDocument(detailHtml);
                var descriptionNode = detailDoc.QuerySelector("div.taleo-description, #requisitionDescriptionInterface.ID1525") ?? detailDoc.Body;
                var descriptionHtml = descriptionNode?.InnerHtml ?? detailHtml;
                var descriptionText = descriptionNode?.TextContent ?? detailDoc.Body?.TextContent ?? string.Empty;

                results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, descriptionHtml, descriptionText, null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl Taleo site {Url}", careersUri);
        }

        return results;
    }
}
