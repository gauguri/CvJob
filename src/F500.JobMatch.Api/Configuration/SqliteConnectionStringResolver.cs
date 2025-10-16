using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace F500.JobMatch.Api.Configuration;

public static class SqliteConnectionStringResolver
{
    private const string DefaultFileName = "jobmatch.db";
    private const string AppFolderName = "F500.JobMatch";

    public static string Resolve(string? configuredConnectionString)
    {
        var builder = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? new SqliteConnectionStringBuilder { DataSource = DefaultFileName }
            : new SqliteConnectionStringBuilder(configuredConnectionString);

        if (IsInMemory(builder))
        {
            return builder.ToString();
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

        return builder.ToString();
    }

    private static bool IsInMemory(SqliteConnectionStringBuilder builder)
    {
        return string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
               || builder.Mode == SqliteOpenMode.Memory;
    }
}
