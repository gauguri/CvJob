using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace F500.JobMatch.Api.Configuration;

public static class SqliteConnectionStringResolver
{
    private const string DefaultFileName = "jobmatch.db";
    private const string AppFolderName = "F500.JobMatch";

    public static SqliteConnectionOptions Resolve(string? configuredConnectionString)
    {
        var builder = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? new SqliteConnectionStringBuilder { DataSource = DefaultFileName }
            : new SqliteConnectionStringBuilder(configuredConnectionString);

        var isInMemory = IsInMemory(builder);
        if (isInMemory)
        {
            if (builder.Cache == SqliteCacheMode.Default)
            {
                builder.Cache = SqliteCacheMode.Shared;
            }

            if (string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
                && builder.Mode == SqliteOpenMode.ReadWriteCreate)
            {
                builder.Mode = SqliteOpenMode.Memory;
            }

            return new SqliteConnectionOptions(builder.ToString(), true);
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = AppContext.BaseDirectory;
            }

            var targetDirectory = Path.Combine(basePath, AppFolderName);
            Directory.CreateDirectory(targetDirectory);
            builder.DataSource = Path.Combine(targetDirectory, builder.DataSource);
        }
        else
        {
            var directory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return new SqliteConnectionOptions(builder.ToString(), false);
    }

    public static bool IsInMemory(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        return IsInMemory(builder);
    }

    private static bool IsInMemory(SqliteConnectionStringBuilder builder)
    {
        return string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
               || builder.Mode == SqliteOpenMode.Memory;
    }
}

public sealed record SqliteConnectionOptions(string ConnectionString, bool IsInMemory);
