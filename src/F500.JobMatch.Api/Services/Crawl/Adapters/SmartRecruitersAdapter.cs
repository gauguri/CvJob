using System.Linq;
using System.Text.Json;

namespace F500.JobMatch.Api.Services.Crawl.Adapters;

public class SmartRecruitersAdapter : BaseAdapter
{
    public SmartRecruitersAdapter(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SmartRecruitersAdapter> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    public override string Name => "SmartRecruiters";

    public override async Task<IReadOnlyList<RawJobPosting>> CrawlAsync(string company, Uri careersUri, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("crawler");
        var results = new List<RawJobPosting>();
        try
        {
            var segments = careersUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return results;
            }
            var companySlug = segments[0];
            var apiUrl = new Uri($"https://api.smartrecruiters.com/v1/companies/{companySlug}/postings");
            var response = await client.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return results;
            }
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var posting in content.EnumerateArray())
            {
                var title = posting.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
                if (!IsProductRole(title))
                {
                    continue;
                }
                var id = posting.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                var location = posting.TryGetProperty("location", out var locationElement) ? locationElement.ToString() : string.Empty;
                var postedAt = posting.TryGetProperty("releasedDate", out var released) && DateTime.TryParse(released.GetString(), out var parsedDate) ? parsedDate : (DateTime?)null;
                var jobUri = id != null ? new Uri(careersUri, id) : careersUri;

                string descriptionHtml = string.Empty;
                string descriptionText = string.Empty;
                if (!string.IsNullOrEmpty(id))
                {
                    var detailResponse = await client.GetAsync(new Uri($"https://api.smartrecruiters.com/v1/companies/{companySlug}/postings/{id}"), cancellationToken);
                    if (detailResponse.IsSuccessStatusCode)
                    {
                        var detailJson = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                        using var detailDoc = JsonDocument.Parse(detailJson);
                        descriptionHtml = detailDoc.RootElement.TryGetProperty("jobAd", out var jobAd) && jobAd.TryGetProperty("sections", out var sections)
                            ? string.Join("\n", sections.EnumerateArray().Select(s => s.TryGetProperty("text", out var text) ? text.GetString() : string.Empty))
                            : string.Empty;
                        descriptionText = descriptionHtml;
                    }
                }

                results.Add(CreatePosting(title, jobUri.AbsoluteUri, company, location, descriptionHtml, descriptionText, null, postedAt));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl SmartRecruiters site {Url}", careersUri);
        }

        return results;
    }
}
