using Fluxo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Infrastructure.Configurations;

public class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(EntityTypeBuilder<DeviceCredential> builder)
    {
        builder.ToTable("device_credentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId)
            .IsRequired();

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.SecretHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.SecretSalt)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.RevokedAtUtc);

        builder.Property(x => x.LastUsedAtUtc);

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.HasIndex(x => x.DeviceId)
            .HasDatabaseName("UX_device_credentials_device_active")
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
