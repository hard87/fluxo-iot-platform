using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class DeviceCredential
{
    private DeviceCredential()
    {
    }

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string SecretHash { get; private set; } = string.Empty;
    public string SecretSalt { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }

    public DeviceCredential(
        Guid deviceId,
        string username,
        string secretHash,
        string secretSalt)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        if (string.IsNullOrWhiteSpace(secretHash))
            throw new ArgumentException("SecretHash is required.", nameof(secretHash));

        if (string.IsNullOrWhiteSpace(secretSalt))
            throw new ArgumentException("SecretSalt is required.", nameof(secretSalt));

        Id = Guid.NewGuid();
        DeviceId = deviceId;
        Username = username.Trim();
        SecretHash = secretHash.Trim();
        SecretSalt = secretSalt.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public DeviceCredentialStatus Status =>
        IsActive ? DeviceCredentialStatus.Active : DeviceCredentialStatus.Revoked;

    public void Revoke(DateTime revokedAtUtc)
    {
        if (!IsActive)
            return;

        IsActive = false;
        RevokedAtUtc = EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
    }

    public void MarkUsed(DateTime usedAtUtc)
    {
        LastUsedAtUtc = EnsureUtc(usedAtUtc, nameof(usedAtUtc));
    }

    private static DateTime EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        throw new ArgumentException("DateTime must include a UTC or Local kind.", paramName);
    }
}
