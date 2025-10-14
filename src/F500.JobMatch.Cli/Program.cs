using Microsoft.AspNetCore.Http;
using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services;
using F500.JobMatch.Api.Services.Crawl;
using F500.JobMatch.Api.Services.Crawl.Adapters;
using F500.JobMatch.Api.Services.Match;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("../F500.JobMatch.Api/appsettings.json", optional: true);

builder.Services.AddDbContext<JobMatchDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=jobmatch.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddLogging(lb => lb.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger()));

builder.Services.AddHttpClient("crawler");
builder.Services.AddHttpClient("robots");

builder.Services.AddScoped<ResumeService>();
builder.Services.AddScoped<CrawlDispatcher>();
builder.Services.AddScoped<RobotsService>();
builder.Services.AddScoped<DedupeService>();
builder.Services.AddScoped<Normalizer>();
builder.Services.AddScoped<Detectors>();
builder.Services.AddScoped<GenericHtmlAdapter>();
builder.Services.AddScoped<WorkdayAdapter>();
builder.Services.AddScoped<GreenhouseAdapter>();
builder.Services.AddScoped<LeverAdapter>();
builder.Services.AddScoped<SmartRecruitersAdapter>();
builder.Services.AddScoped<SuccessFactorsAdapter>();
builder.Services.AddScoped<TaleoAdapter>();
builder.Services.AddScoped<IcimsAdapter>();
builder.Services.AddScoped<TextClean>();
builder.Services.AddScoped<TfIdfVectorizer>();
builder.Services.AddScoped<MatchScoring>();
builder.Services.AddScoped<ExplainService>();

using var host = builder.Build();

var serviceProvider = host.Services;
using var scope = serviceProvider.CreateScope();
var services = scope.ServiceProvider;

await services.GetRequiredService<JobMatchDbContext>().Database.MigrateAsync();

if (args.Length == 0)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  ingest-resume <path>");
    Console.WriteLine("  crawl --csv <path> [--limit <n>] [--all]");
    Console.WriteLine("  match <resumeId> [--top <n>]");
    return;
}

switch (args[0])
{
    case "ingest-resume":
        await IngestResumeAsync(services, args);
        break;
    case "crawl":
        await RunCrawlAsync(services, args);
        break;
    case "match":
        await RunMatchAsync(services, args);
        break;
    default:
        Console.WriteLine("Unknown command");
        break;
}

static async Task IngestResumeAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Path required");
        return;
    }
    var fileInfo = new FileInfo(args[1]);
    if (!fileInfo.Exists)
    {
        Console.WriteLine("File not found");
        return;
    }
    await using var stream = fileInfo.OpenRead();
    var formFile = new FormFile(stream, 0, stream.Length, "file", fileInfo.Name);
    var resumeService = services.GetRequiredService<ResumeService>();
    var id = await resumeService.SaveResumeAsync(formFile);
    Console.WriteLine($"Saved resume {id}");
}

static async Task RunCrawlAsync(IServiceProvider services, string[] args)
{
    var csvPath = "data/fortune500.sample.csv";
    int limit = 50;
    bool freshOnly = true;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--csv" && i + 1 < args.Length)
        {
            csvPath = args[++i];
        }
        else if (args[i] == "--limit" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed))
        {
            limit = parsed;
            i++;
        }
        else if (args[i] == "--all")
        {
            freshOnly = false;
        }
    }
    var dispatcher = services.GetRequiredService<CrawlDispatcher>();
    var summaries = await dispatcher.RunAsync(new CrawlRequest { CsvPath = csvPath, LimitCompanies = limit, FreshOnly = freshOnly }, CancellationToken.None);
    foreach (var summary in summaries)
    {
        Console.WriteLine($"{summary.Company}: stored {summary.Stored}, skipped {summary.Skipped}, notes: {summary.Notes}");
    }
}

static async Task RunMatchAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 2 || !Guid.TryParse(args[1], out var resumeId))
    {
        Console.WriteLine("Resume ID required");
        return;
    }
    int top = 10;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--top" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed))
        {
            top = parsed;
        }
    }
    var scoring = services.GetRequiredService<MatchScoring>();
    var explain = services.GetRequiredService<ExplainService>();
    var resumeService = services.GetRequiredService<ResumeService>();
    var resume = await resumeService.GetResumeAsync(resumeId);
    if (resume == null)
    {
        Console.WriteLine("Resume not found");
        return;
    }
    var results = await scoring.ScoreTopAsync(resumeId, top, CancellationToken.None);
    foreach (var result in results)
    {
        Console.WriteLine($"{result.Posting.Title} @ {result.Posting.Company} -> {result.Score:F1}");
        foreach (var bullet in explain.BuildExplanation(result, resume))
        {
            Console.WriteLine($"  - {bullet}");
        }
        Console.WriteLine($"  Link: {result.Posting.Url}");
        Console.WriteLine();
    }
}
