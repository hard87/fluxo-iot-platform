using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;
public sealed class MetricDefinitionDiscoveryAuditConfiguration : IEntityTypeConfiguration<MetricDefinitionDiscoveryAudit>
{
    public void Configure(EntityTypeBuilder<MetricDefinitionDiscoveryAudit> b)
    {
        b.ToTable("metric_definition_discovery_audits"); b.HasKey(x => x.Id);
        b.Property(x => x.TenantId).HasMaxLength(120).IsRequired(); b.Property(x => x.DeviceId).HasMaxLength(160).IsRequired();
        b.Property(x => x.MetricKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.DiscoveredAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.WorkspaceId, x.DeviceId, x.DiscoveredAtUtc });
        b.HasOne<TelemetryIngestionRecord>().WithMany().HasForeignKey(x => x.IngestionRecordId).OnDelete(DeleteBehavior.Cascade);
    }
}
