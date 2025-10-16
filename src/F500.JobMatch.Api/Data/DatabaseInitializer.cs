using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace F500.JobMatch.Api.Data;

public static class DatabaseInitializer
{
    public static Task EnsureDatabaseCreatedAsync(JobMatchDbContext context, CancellationToken cancellationToken = default)
    {
        if (context.Database.IsSqlite())
        {
            return context.Database.EnsureCreatedAsync(cancellationToken);
        }

        return context.Database.MigrateAsync(cancellationToken);
    }

    public static void EnsureDatabaseCreated(JobMatchDbContext context)
    {
        if (context.Database.IsSqlite())
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }
    }
}
