using System;
using System.Data.Common;
using F500.JobMatch.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace F500.JobMatch.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobMatchDatabase(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("A database connection string must be configured.");
        }

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        string? provider = null;
        if (builder.TryGetValue("Provider", out var providerValue))
        {
            provider = providerValue?.ToString();
            builder.Remove("Provider");
            connectionString = builder.ConnectionString;
        }

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<JobMatchDbContext>(options =>
            {
                options.UseSqlite(connectionString);
            });
        }
        else if (provider is null || string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<JobMatchDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(JobMatchDbContext).Assembly.FullName);
                });
            });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }

        return services;
    }
}
