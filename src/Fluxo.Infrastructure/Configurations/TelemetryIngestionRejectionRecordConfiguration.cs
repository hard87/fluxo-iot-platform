using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;

public class TelemetryIngestionRejectionRecordConfiguration : IEntityTypeConfiguration<TelemetryIngestionRejectionRecord>
{
    public void Configure(EntityTypeBuilder<TelemetryIngestionRejectionRecord> builder)
    {
        builder.ToTable("telemetry_ingestion_rejections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        builder.Property(x => x.Topic)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.PayloadRaw)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.ErrorType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.TenantId)
            .HasMaxLength(120);

        builder.Property(x => x.WorkspaceId);

        builder.Property(x => x.DeviceId)
            .HasMaxLength(120);

        builder.Property(x => x.MessageType)
            .HasMaxLength(60);

        builder.Property(x => x.Sequence);

        builder.HasIndex(x => x.ReceivedAtUtc);
        builder.HasIndex(x => x.ErrorType);
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.DeviceId, x.ReceivedAtUtc })
            .HasDatabaseName("IX_tirj_tenant_workspace_device_received_at");
    }
}
