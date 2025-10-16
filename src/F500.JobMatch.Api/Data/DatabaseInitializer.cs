using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace F500.JobMatch.Api.Data;

public static class DatabaseInitializer
{
    public static Task EnsureDatabaseCreatedAsync(JobMatchDbContext context, CancellationToken cancellationToken = default)
    {
        return context.Database.MigrateAsync(cancellationToken);
    }

    public static void EnsureDatabaseCreated(JobMatchDbContext context)
    {
        context.Database.Migrate();
    }
}
