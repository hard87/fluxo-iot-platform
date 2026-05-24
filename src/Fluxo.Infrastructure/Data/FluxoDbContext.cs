using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Data;

public class FluxoDbContext : DbContext
{
    public FluxoDbContext(DbContextOptions<FluxoDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
    public DbSet<TelemetryIngestionRecord> TelemetryIngestionRecords => Set<TelemetryIngestionRecord>();
    public DbSet<TelemetryIngestionRejectionRecord> TelemetryIngestionRejectionRecords =>
        Set<TelemetryIngestionRejectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FluxoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
