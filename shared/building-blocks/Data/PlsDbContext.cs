using Microsoft.EntityFrameworkCore;

namespace PLS.BuildingBlocks.Data;

public sealed class PlsDbContext : DbContext
{
    public PlsDbContext(DbContextOptions<PlsDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<SummaryJob> SummaryJobs => Set<SummaryJob>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<RetrievalRun> RetrievalRuns => Set<RetrievalRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<Subscription>().HasKey(x => x.Id);
        modelBuilder.Entity<Document>().HasKey(x => x.Id);
        modelBuilder.Entity<SummaryJob>().HasKey(x => x.Id);
        modelBuilder.Entity<Summary>().HasKey(x => x.Id);
        modelBuilder.Entity<AuditLog>().HasKey(x => x.Id);
        modelBuilder.Entity<DocumentChunk>().HasKey(x => x.Id);
        modelBuilder.Entity<RetrievalRun>().HasKey(x => x.Id);

        modelBuilder.Entity<User>()
            .HasIndex(x => new { x.TenantId, x.ExternalId })
            .IsUnique();

        modelBuilder.Entity<Subscription>()
            .HasIndex(x => x.TenantId);

        modelBuilder.Entity<Document>()
            .HasIndex(x => new { x.TenantId, x.UserId });

        modelBuilder.Entity<SummaryJob>()
            .HasIndex(x => new { x.TenantId, x.UserId, x.DocumentId });

        modelBuilder.Entity<Summary>()
            .HasIndex(x => new { x.TenantId, x.UserId, x.DocumentId });

        modelBuilder.Entity<DocumentChunk>()
            .HasIndex(x => new { x.TenantId, x.DocumentId });

        modelBuilder.Entity<RetrievalRun>()
            .HasIndex(x => new { x.TenantId, x.DocumentId });
    }
}
