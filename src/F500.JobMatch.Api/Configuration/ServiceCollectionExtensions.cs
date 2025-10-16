using System;
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
            throw new InvalidOperationException("A SQL Server connection string must be configured.");
        }

        services.AddDbContext<JobMatchDbContext>(options =>
        {
            options.UseSqlServer(connectionString, builder =>
            {
                builder.MigrationsAssembly(typeof(JobMatchDbContext).Assembly.FullName);
            });
        });

        return services;
    }
}
