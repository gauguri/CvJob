using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xceed.Words.NET;
using F500.JobMatch.Api.Data;

namespace F500.JobMatch.Api.Services;

public class ResumeService
{
    private readonly JobMatchDbContext _dbContext;

    public ResumeService(JobMatchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> SaveResumeAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Resume file is empty", nameof(file));
        }

        string text = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => await ExtractPdfAsync(file, cancellationToken),
            ".docx" => await ExtractDocxAsync(file, cancellationToken),
            _ => await ExtractTextAsync(file, cancellationToken)
        };

        text = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Unable to extract text from resume");
        }

        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            Text = text,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.Resumes.Add(resume);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return resume.Id;
    }

    public async Task<Resume?> GetResumeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Resumes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    private static async Task<string> ExtractTextAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ExtractDocxAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        using var document = DocX.Load(stream);
        return document.Text;
    }

    private static async Task<string> ExtractPdfAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        var builder = new StringBuilder();
        using var pdf = PdfDocument.Open(stream);
        foreach (Page page in pdf.GetPages())
        {
            builder.AppendLine(page.Text);
        }
        return builder.ToString();
    }

    private static string NormalizeWhitespace(string input)
    {
        return string.Join('\n', input
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()))
            .Trim();
    }
}
