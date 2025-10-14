using RobotsTxt;

namespace F500.JobMatch.Api.Services.Crawl;

public class RobotsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RobotsService> _logger;

    public RobotsService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<RobotsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken)
    {
        var ignoreRobots = _configuration.GetSection("Crawl").GetValue<bool>("IgnoreRobots");
        if (ignoreRobots)
        {
            return true;
        }

        var client = _httpClientFactory.CreateClient("robots");
        var robotsUrl = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/robots.txt");
        try
        {
            var response = await client.GetAsync(robotsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return true; // Assume allowed if robots cannot be fetched
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var robots = Robots.Load(content);
            var userAgent = _configuration.GetSection("Crawl").GetValue<string>("UserAgent") ?? "f500-jobmatch-bot/1.0";
            return robots.IsPathAllowed(userAgent, uri.AbsolutePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate robots.txt for {Url}", uri);
            return false;
        }
    }
}
