using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public abstract class BaseAdapter
{
    private readonly Regex _titleRegex;
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly IConfiguration Configuration;
    protected readonly ILogger _logger;
    protected readonly HtmlParser Parser = new();

    protected BaseAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        Configuration = configuration;
        _logger = logger;
        var pattern = configuration.GetSection("Crawl").GetValue<string>("TitleIncludeRegex") ?? "product";
        _titleRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public abstract string Name { get; }

    public abstract Task<IReadOnlyList<RawJobPosting>> CrawlAsync(string company, Uri careersUri, CancellationToken cancellationToken);

    protected bool IsProductRole(string title)
    {
        return _titleRegex.IsMatch(title);
    }

    protected RawJobPosting CreatePosting(string title, string url, string company, string location, string? html, string text, string? employmentType, DateTime? postedAt)
    {
        return new RawJobPosting
        {
            Title = title.Trim(),
            Location = location.Trim(),
            DescriptionHtml = html,
            DescriptionText = text.Trim(),
            EmploymentType = employmentType,
            PostedAtUtc = postedAt,
            Url = url,
            Source = Name
        };
    }

    protected IEnumerable<RawJobPosting> ParseStructuredData(string html, string company, Uri baseUri)
    {
        var results = new List<RawJobPosting>();

        try
        {
            var document = Parser.ParseDocument(html);
            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
            {
                if (string.IsNullOrWhiteSpace(script.TextContent))
                {
                    continue;
                }

                using var json = JsonDocument.Parse(script.TextContent);
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("@type", out var type))
                {
                    if (string.Equals(type.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase))
                    {
                        var posting = MapJsonJob(json.RootElement, company, baseUri);
                        if (posting != null)
                        {
                            results.Add(posting);
                        }
                    }
                    else if (json.RootElement.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in graph.EnumerateArray())
                        {
                            if (element.TryGetProperty("@type", out var nestedType) && string.Equals(nestedType.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase))
                            {
                                var posting = MapJsonJob(element, company, baseUri);
                                if (posting != null)
                                {
                                    results.Add(posting);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse structured job data");
        }

        return results;
    }

    private RawJobPosting? MapJsonJob(JsonElement element, string company, Uri baseUri)
    {
        if (!element.TryGetProperty("title", out var titleElement))
        {
            return null;
        }
        var title = titleElement.GetString() ?? string.Empty;
        if (!IsProductRole(title))
        {
            return null;
        }

        string location = string.Empty;
        if (element.TryGetProperty("jobLocation", out var jobLocation))
        {
            location = jobLocation.ValueKind switch
            {
                JsonValueKind.String => jobLocation.GetString() ?? string.Empty,
                JsonValueKind.Object => jobLocation.TryGetProperty("address", out var address) ? address.ToString() : jobLocation.ToString(),
                JsonValueKind.Array => string.Join(", ", jobLocation.EnumerateArray().Select(j => j.ToString())),
                _ => string.Empty
            };
        }

        string description = element.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty;
        string employmentType = element.TryGetProperty("employmentType", out var type) ? type.GetString() ?? string.Empty : string.Empty;
        DateTime? posted = null;
        if (element.TryGetProperty("datePosted", out var datePosted) && DateTime.TryParse(datePosted.GetString(), out var parsedDate))
        {
            posted = parsedDate;
        }

        string url = element.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? baseUri.AbsoluteUri : baseUri.AbsoluteUri;
        if (Uri.TryCreate(baseUri, url, out var absoluteUrl))
        {
            url = absoluteUrl.AbsoluteUri;
        }

        return CreatePosting(title, url, company, location, description, description, employmentType, posted);
    }
}
