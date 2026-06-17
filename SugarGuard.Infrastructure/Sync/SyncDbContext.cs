using Microsoft.EntityFrameworkCore;

namespace SugarGuard.Infrastructure.Sync;

public class SyncDbContext : DbContext
{
    public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options) { }

    public DbSet<SyncMeasurement> Measurements { get; set; } = null!;
    public DbSet<SyncLogEntry> SyncLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SyncMeasurement>(entity =>
        {
            entity.HasKey(e => e.MeasurementId);
            entity.HasIndex(e => new { e.ChildId, e.MeasuredAt });
        });

        modelBuilder.Entity<SyncLogEntry>(entity =>
        {
            entity.HasKey(e => e.SyncLogId);
            entity.HasIndex(e => e.MeasurementId);
        });
    }
}
