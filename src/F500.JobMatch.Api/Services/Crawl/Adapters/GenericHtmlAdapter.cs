using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class GenericHtmlAdapter : BaseAdapter
{
    private readonly IConfiguration _configuration;

    public GenericHtmlAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GenericHtmlAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
        _configuration = configuration;
    }

    public override string Name => "GenericHtml";

    public override async Task<IReadOnlyList<RawJobPosting>> CrawlAsync(string company, Uri careersUri, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("crawler");
        var maxPages = _configuration.GetSection("Crawl").GetValue<int?>("MaxPagesPerSite") ?? 5;
        var queue = new Queue<Uri>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<RawJobPosting>();
        queue.Enqueue(careersUri);

        while (queue.Count > 0 && visited.Count < maxPages)
        {
            var next = queue.Dequeue();
            if (!visited.Add(next.AbsoluteUri))
            {
                continue;
            }

            try
            {
                var response = await client.GetAsync(next, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var document = Parser.ParseDocument(html);
                foreach (var link in ExtractJobLinks(document, next))
                {
                    if (results.Count >= 50)
                    {
                        break;
                    }

                    var posting = await FetchJobAsync(client, company, link, cancellationToken);
                    if (posting != null && IsProductRole(posting.Title))
                    {
                        results.Add(posting);
                    }
                }

                foreach (var child in document.QuerySelectorAll("a"))
                {
                    var href = child.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        continue;
                    }
                    if (!Uri.TryCreate(next, href, out var childUri))
                    {
                        continue;
                    }
                    if (!string.Equals(childUri.Host, careersUri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!visited.Contains(childUri.AbsoluteUri) && queue.Count + visited.Count < maxPages)
                    {
                        if (Regex.IsMatch(href, "(?i)(job|careers|opening|position)"))
                        {
                            queue.Enqueue(childUri);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to crawl {Url}", next);
            }
        }

        return results;
    }

    private IEnumerable<Uri> ExtractJobLinks(IDocument document, Uri baseUri)
    {
        foreach (var anchor in document.QuerySelectorAll("a"))
        {
            var href = anchor.GetAttribute("href");
            var text = (anchor.TextContent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!IsProductRole(text))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, href, out var uri))
            {
                continue;
            }

            yield return uri;
        }
    }

    private async Task<RawJobPosting?> FetchJobAsync(HttpClient client, string company, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = Parser.ParseDocument(html);
            var title = document.QuerySelector("h1, h2")?.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = document.Title ?? uri.AbsoluteUri;
            }

            var location = document.QuerySelector("[class*=location], [data-location], .job-location")?.TextContent?.Trim() ?? string.Empty;
            var descriptionElement = document.QuerySelector("article, .job-description, #job-description") ?? document.Body;
            var descriptionHtml = descriptionElement?.InnerHtml ?? html;
            var descriptionText = descriptionElement?.TextContent ?? document.Body?.TextContent ?? string.Empty;
            var employmentType = document.QuerySelector("[class*=type], .employment-type")?.TextContent?.Trim();

            return CreatePosting(title, uri.AbsoluteUri, company, location, descriptionHtml, descriptionText, employmentType, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch job detail {Url}", uri);
            return null;
        }
    }
}
