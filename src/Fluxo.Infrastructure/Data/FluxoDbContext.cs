using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Data;

public class FluxoDbContext : DbContext
{
    public FluxoDbContext(DbContextOptions<FluxoDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
    public DbSet<TelemetryIngestionRecord> TelemetryIngestionRecords => Set<TelemetryIngestionRecord>();
    public DbSet<TelemetryIngestionRejectionRecord> TelemetryIngestionRejectionRecords =>
        Set<TelemetryIngestionRejectionRecord>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<TelemetryPoint> TelemetryPoints => Set<TelemetryPoint>();
    public DbSet<MetricDefinitionDiscoveryAudit> MetricDefinitionDiscoveryAudits => Set<MetricDefinitionDiscoveryAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FluxoDbContext).Assembly);
        Alerts.AlertModelConfiguration.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
