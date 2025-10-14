using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using F500.JobMatch.Api.Data;

namespace F500.JobMatch.Api.Services.Crawl;

public class DedupeService
{
    private readonly JobMatchDbContext _dbContext;

    public DedupeService(JobMatchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string ComputeStableHash(string company, string title, string url)
    {
        var normalized = string.Join('|', company.Trim().ToLowerInvariant(), title.Trim().ToLowerInvariant(), url.Trim().ToLowerInvariant());
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    public async Task<bool> ExistsAsync(string stableIdHash, CancellationToken cancellationToken)
    {
        return await _dbContext.JobPostings.AnyAsync(p => p.StableIdHash == stableIdHash, cancellationToken);
    }

    public void Track(JobPosting posting)
    {
        _dbContext.JobPostings.Add(posting);
    }
}
