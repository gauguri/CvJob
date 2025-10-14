using System.Net;
using System.Reflection;
using AngleSharp;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Serilog;
using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Middleware;
using F500.JobMatch.Api.Services;
using F500.JobMatch.Api.Services.Crawl;
using F500.JobMatch.Api.Services.Crawl.Adapters;
using F500.JobMatch.Api.Services.Match;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console();
});

builder.Services.AddDbContext<JobMatchDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=jobmatch.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.Configure<FormOptions>(opts =>
{
    opts.MultipartBodyLengthLimit = 50L * 1024L * 1024L; // 50 MB
});

builder.Services.AddHttpClient("crawler")
    .ConfigureHttpClient((sp, client) =>
    {
        var crawlSection = sp.GetRequiredService<IConfiguration>().GetSection("Crawl");
        client.Timeout = TimeSpan.FromSeconds(crawlSection.GetValue<int?>("TimeoutSeconds") ?? 30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(crawlSection.GetValue<string>("UserAgent") ?? "f500-jobmatch-bot/1.0");
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 3)));

builder.Services.AddHttpClient("robots")
    .ConfigureHttpClient((sp, client) =>
    {
        var crawlSection = sp.GetRequiredService<IConfiguration>().GetSection("Crawl");
        client.Timeout = TimeSpan.FromSeconds(crawlSection.GetValue<int?>("TimeoutSeconds") ?? 15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(crawlSection.GetValue<string>("UserAgent") ?? "f500-jobmatch-bot/1.0");
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 2)));

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
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapRazorPages();

await EnsureDatabaseAsync(app.Services, app.Logger);

app.Run();

static async Task EnsureDatabaseAsync(IServiceProvider services, ILogger logger)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
    logger.Information("Applying database migrations");
    await db.Database.MigrateAsync();
}
