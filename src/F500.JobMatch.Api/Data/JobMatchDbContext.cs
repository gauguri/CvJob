using Microsoft.EntityFrameworkCore;

namespace F500.JobMatch.Api.Data;

public class JobMatchDbContext : DbContext
{
    public JobMatchDbContext(DbContextOptions<JobMatchDbContext> options) : base(options)
    {
    }

    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobPosting>()
            .HasIndex(p => p.StableIdHash)
            .IsUnique();

        modelBuilder.Entity<JobPosting>()
            .Property(p => p.StableIdHash)
            .IsRequired()
            .HasMaxLength(64);

        base.OnModelCreating(modelBuilder);
    }
}
