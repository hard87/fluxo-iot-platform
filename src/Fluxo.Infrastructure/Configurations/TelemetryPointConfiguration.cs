using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;
public sealed class TelemetryPointConfiguration : IEntityTypeConfiguration<TelemetryPoint>
{
    public void Configure(EntityTypeBuilder<TelemetryPoint> b)
    {
        b.ToTable("telemetry_points", t => t.HasCheckConstraint("CK_telemetry_points_exactly_one_value",
            "((\"NumericValue\" IS NOT NULL)::int + (\"BooleanValue\" IS NOT NULL)::int + (\"TextValue\" IS NOT NULL)::int) = 1"));
        b.HasKey(x => new { x.OccurredAtUtc, x.Id }); b.Property(x => x.TenantId).HasMaxLength(120).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(160).IsRequired(); b.Property(x => x.TextValue).HasMaxLength(256);
        b.HasOne<MetricDefinition>().WithMany().HasForeignKey(x => x.MetricDefinitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TelemetryIngestionRecord>().WithMany().HasForeignKey(x => x.IngestionRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.WorkspaceId, x.DeviceId, x.MetricDefinitionId, x.OccurredAtUtc });
    }
}
