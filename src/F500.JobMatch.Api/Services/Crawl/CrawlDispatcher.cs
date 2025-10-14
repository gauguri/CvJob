using Microsoft.Extensions.DependencyInjection;
using System.Text;
using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services.Crawl.Adapters;

namespace F500.JobMatch.Api.Services.Crawl;

public class CrawlDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly JobMatchDbContext _dbContext;
    private readonly Normalizer _normalizer;
    private readonly DedupeService _dedupeService;
    private readonly RobotsService _robotsService;
    private readonly Detectors _detectors;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CrawlDispatcher> _logger;

    public CrawlDispatcher(IServiceProvider serviceProvider,
        JobMatchDbContext dbContext,
        Normalizer normalizer,
        DedupeService dedupeService,
        RobotsService robotsService,
        Detectors detectors,
        IHttpClientFactory httpClientFactory,
        ILogger<CrawlDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;
        _normalizer = normalizer;
        _dedupeService = dedupeService;
        _robotsService = robotsService;
        _detectors = detectors;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CrawlSummaryDto>> RunAsync(CrawlRequest request, CancellationToken cancellationToken)
    {
        if (!File.Exists(request.CsvPath))
        {
            throw new FileNotFoundException("CSV file not found", request.CsvPath);
        }

        var summaries = new List<CrawlSummaryDto>();
        var lines = await File.ReadAllLinesAsync(request.CsvPath, Encoding.UTF8, cancellationToken);
        int processed = 0;
        foreach (var line in lines)
        {
            if (processed >= request.LimitCompanies)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            var columns = line.Split(',', 3);
            if (columns.Length < 2)
            {
                continue;
            }

            var company = columns[0].Trim();
            var url = columns[1].Trim();
            var notes = columns.Length > 2 ? columns[2].Trim() : string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var careersUri))
            {
                _logger.LogWarning("Invalid URL for company {Company}: {Url}", company, url);
                continue;
            }

            processed++;
            var summary = await ProcessCompanyAsync(company, careersUri, notes, request.FreshOnly, cancellationToken);
            summaries.Add(summary);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return summaries;
    }

    private async Task<CrawlSummaryDto> ProcessCompanyAsync(string company, Uri careersUri, string notes, bool freshOnly, CancellationToken cancellationToken)
    {
        int scanned = 0;
        int fetched = 0;
        int stored = 0;
        int skipped = 0;
        var client = _httpClientFactory.CreateClient("crawler");
        string summaryNotes = notes;

        if (!await _robotsService.IsAllowedAsync(careersUri, cancellationToken))
        {
            summaryNotes = AppendNote(summaryNotes, "Blocked by robots.txt");
            return new CrawlSummaryDto
            {
                Domain = careersUri.Host,
                Company = company,
                Scanned = scanned,
                Fetched = fetched,
                Stored = stored,
                Skipped = skipped,
                Notes = summaryNotes
            };
        }

        AtsType atsType = AtsType.Unknown;
        try
        {
            atsType = await _detectors.DetectAsync(client, careersUri.AbsoluteUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect ATS for {Company}", company);
        }

        using var scope = _serviceProvider.CreateScope();
        BaseAdapter adapter = ResolveAdapter(scope.ServiceProvider, atsType);
        IReadOnlyList<RawJobPosting> rawPostings = Array.Empty<RawJobPosting>();
        try
        {
            rawPostings = await adapter.CrawlAsync(company, careersUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Adapter {Adapter} failed for {Company}", adapter.Name, company);
            summaryNotes = AppendNote(summaryNotes, $"Adapter {adapter.Name} error");
        }

        scanned = rawPostings.Count;
        foreach (var raw in rawPostings)
        {
            fetched++;
            var posting = _normalizer.Normalize(company, raw);
            if (string.IsNullOrWhiteSpace(posting.DescriptionText))
            {
                skipped++;
                continue;
            }

            if (freshOnly && await _dedupeService.ExistsAsync(posting.StableIdHash, cancellationToken))
            {
                skipped++;
                continue;
            }

            _dedupeService.Track(posting);
            stored++;
        }

        return new CrawlSummaryDto
        {
            Domain = careersUri.Host,
            Company = company,
            Scanned = scanned,
            Fetched = fetched,
            Stored = stored,
            Skipped = skipped,
            Notes = AppendNote(summaryNotes, atsType == AtsType.Unknown ? "Generic" : atsType.ToString())
        };
    }

    private static BaseAdapter ResolveAdapter(IServiceProvider provider, AtsType atsType)
    {
        return atsType switch
        {
            AtsType.Workday => provider.GetRequiredService<WorkdayAdapter>(),
            AtsType.Greenhouse => provider.GetRequiredService<GreenhouseAdapter>(),
            AtsType.Lever => provider.GetRequiredService<LeverAdapter>(),
            AtsType.SmartRecruiters => provider.GetRequiredService<SmartRecruitersAdapter>(),
            AtsType.SuccessFactors => provider.GetRequiredService<SuccessFactorsAdapter>(),
            AtsType.Taleo => provider.GetRequiredService<TaleoAdapter>(),
            AtsType.Icims => provider.GetRequiredService<IcimsAdapter>(),
            _ => provider.GetRequiredService<GenericHtmlAdapter>()
        };
    }

    private static string AppendNote(string existing, string note)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return note;
        }
        if (string.IsNullOrWhiteSpace(note))
        {
            return existing;
        }
        return existing + "; " + note;
    }
}
