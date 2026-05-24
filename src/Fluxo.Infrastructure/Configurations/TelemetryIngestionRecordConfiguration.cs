using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;

public class TelemetryIngestionRecordConfiguration : IEntityTypeConfiguration<TelemetryIngestionRecord>
{
    public void Configure(EntityTypeBuilder<TelemetryIngestionRecord> builder)
    {
        builder.ToTable("telemetry_ingestion_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.WorkspaceId)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.MessageType)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(x => x.Topic)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.SchemaVersion)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.FirmwareVersion)
            .HasMaxLength(40);

        builder.Property(x => x.Sequence);

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Temperature);
        builder.Property(x => x.Humidity);
        builder.Property(x => x.Battery);
        builder.Property(x => x.Rssi);
        builder.Property(x => x.UptimeSec);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId });
        builder.HasIndex(x => new { x.WorkspaceId, x.DeviceId });
        builder.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.DeviceId, x.OccurredAtUtc })
            .HasDatabaseName("IX_tir_tenant_workspace_device_occurred_at");
        builder.HasIndex(x => x.ReceivedAtUtc);
        builder.HasIndex(x => new { x.TenantId, x.WorkspaceId, x.DeviceId, x.Sequence })
            .HasDatabaseName("UX_telemetry_ingestion_records_tenant_workspace_device_sequence")
            .IsUnique()
            .HasFilter("\"Sequence\" IS NOT NULL");
    }
}
