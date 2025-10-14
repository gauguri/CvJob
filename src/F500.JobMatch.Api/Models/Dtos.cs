namespace F500.JobMatch.Api.Models;

public record ResumeUploadResponse(Guid ResumeId);

public record CrawlRequest
{
    public string CsvPath { get; init; } = "data/fortune500.sample.csv";
    public int LimitCompanies { get; init; } = 50;
    public bool FreshOnly { get; init; } = true;
}

public record CrawlSummaryDto
{
    public string Domain { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public int Scanned { get; init; };
    public int Fetched { get; init; };
    public int Stored { get; init; };
    public int Skipped { get; init; };
    public string Notes { get; init; } = string.Empty;
}

public record CrawlResponse(IReadOnlyList<CrawlSummaryDto> Summaries);

public record MatchResultDto
{
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? Location { get; init; };
    public string Url { get; init; } = string.Empty;
    public string? Source { get; init; };
    public double MatchScore { get; init; };
    public IReadOnlyList<string> Explanation { get; init; } = Array.Empty<string>();
}
