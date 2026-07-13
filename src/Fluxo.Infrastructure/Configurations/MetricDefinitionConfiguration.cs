using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;
public sealed class MetricDefinitionConfiguration : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> b)
    {
        b.ToTable("metric_definitions"); b.HasKey(x => x.Id);
        b.Property(x => x.TenantId).HasMaxLength(120).IsRequired(); b.Property(x => x.MetricKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(128).IsRequired(); b.Property(x => x.SemanticType).HasMaxLength(64);
        b.Property(x => x.CanonicalUnit).HasMaxLength(32); b.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16); b.HasIndex(x => new { x.WorkspaceId, x.MetricKey }).IsUnique();
    }
}
