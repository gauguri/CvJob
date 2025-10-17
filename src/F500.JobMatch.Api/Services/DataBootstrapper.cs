using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services.Crawl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace F500.JobMatch.Api.Services;

public interface IDataBootstrapper
{
    Task EnsureJobPostingsAsync(CancellationToken cancellationToken = default);
}

public sealed class DataBootstrapper : IDataBootstrapper
{
    private const int DefaultCompanyLimit = 50;

    private readonly JobMatchDbContext _dbContext;
    private readonly CrawlDispatcher _crawlDispatcher;
    private readonly ILogger<DataBootstrapper> _logger;
    private readonly string _csvPath;
    private readonly int _companyLimit;
    private readonly bool _freshOnly;

    public DataBootstrapper(
        JobMatchDbContext dbContext,
        CrawlDispatcher crawlDispatcher,
        IConfiguration configuration,
        ILogger<DataBootstrapper> logger)
    {
        _dbContext = dbContext;
        _crawlDispatcher = crawlDispatcher;
        _logger = logger;

        var bootstrapSection = configuration.GetSection("Bootstrap");
        _csvPath = bootstrapSection.GetValue<string>("CsvPath") ?? "data/fortune500.sample.csv";
        _companyLimit = bootstrapSection.GetValue<int?>("CompanyLimit") ?? DefaultCompanyLimit;
        _freshOnly = bootstrapSection.GetValue<bool?>("FreshOnly") ?? true;
    }

    public async Task EnsureJobPostingsAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.JobPostings.AnyAsync(cancellationToken))
        {
            return;
        }

        var resolvedCsvPath = ResolveExistingPath(_csvPath);
        if (!File.Exists(resolvedCsvPath))
        {
            _logger.LogWarning(
                "Skipping bootstrap crawl because CSV path {CsvPath} was not found.",
                resolvedCsvPath);
            return;
        }

        try
        {
            _logger.LogInformation(
                "No job postings found. Bootstrapping from {CsvPath} (limit {Limit}, freshOnly: {FreshOnly}).",
                resolvedCsvPath,
                _companyLimit,
                _freshOnly);

            await _crawlDispatcher.RunAsync(new CrawlRequest
            {
                CsvPath = resolvedCsvPath,
                LimitCompanies = _companyLimit,
                FreshOnly = _freshOnly
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bootstrap crawl failed. Matches will remain empty until jobs are ingested.");
        }
    }

    private static string ResolveExistingPath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        foreach (var candidate in EnumerateCandidatePaths(path))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return path;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string relativePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var baseDir in GetBaseDirectories())
        {
            var current = baseDir;
            for (var depth = 0; depth < 6; depth++)
            {
                var candidateBase = Path.GetFullPath(current);
                if (seen.Add(candidateBase))
                {
                    yield return Path.GetFullPath(Path.Combine(candidateBase, relativePath));
                }

                var parent = Path.GetFullPath(Path.Combine(current, ".."));
                if (string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = parent;
            }
        }

        yield return Path.GetFullPath(relativePath);
    }

    private static IEnumerable<string> GetBaseDirectories()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            yield return baseDirectory;
        }
    }
}
