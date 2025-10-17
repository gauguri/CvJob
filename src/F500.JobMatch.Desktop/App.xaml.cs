using System;
using System.IO;
using System.Windows;
using F500.JobMatch.Api.Configuration;
using F500.JobMatch.Api.Data;
using F500.JobMatch.Api.Services;
using F500.JobMatch.Api.Services.Crawl;
using F500.JobMatch.Api.Services.Crawl.Adapters;
using F500.JobMatch.Api.Services.Match;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace F500.JobMatch.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(e.Args);

        var apiSettingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "F500.JobMatch.Api", "appsettings.json"));
        if (File.Exists(apiSettingsPath))
        {
            builder.Configuration.AddJsonFile(apiSettingsPath, optional: true);
        }

        var connectionString = builder.Configuration.GetConnectionString("Default");

        builder.Services.AddJobMatchDatabase(connectionString);

        builder.Services.AddLogging(lb => lb.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger()));

        builder.Services.AddHttpClient("crawler");
        builder.Services.AddHttpClient("robots");

        builder.Services.AddScoped<ResumeService>();
        builder.Services.AddScoped<IDataBootstrapper, DataBootstrapper>();
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

        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
        DatabaseInitializer.EnsureDatabaseCreated(db);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
