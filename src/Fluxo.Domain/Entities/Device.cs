using System.Text.Json;
using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class Device
{
    public const string DefaultTenantId = "default";

    private Device()
    {
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = DefaultTenantId;
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Identifier { get; private set; } = string.Empty;
    public DeviceCategory Category { get; private set; }
    public string? MetadataJson { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastContactAtUtc { get; private set; }
    public DateTime? LastTelemetryReceivedAtUtc { get; private set; }
    public DateTime? LastTelemetryOccurredAtUtc { get; private set; }
    public long? LastTelemetrySequence { get; private set; }
    public string? LastTelemetryPayloadJson { get; private set; }

    public Device(
        Guid workspaceId,
        string name,
        string identifier,
        DeviceCategory category,
        string? metadataJson = null,
        string? tenantId = null)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier is required.", nameof(identifier));

        if (!Enum.IsDefined(category))
            throw new ArgumentException("Category is invalid.", nameof(category));

        ValidateJsonIfProvided(metadataJson, nameof(metadataJson));

        Id = Guid.NewGuid();
        TenantId = NormalizeTenantId(tenantId);
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Identifier = identifier.Trim();
        Category = category;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));

        Name = newName.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void UpdateMetadata(string? metadataJson)
    {
        ValidateJsonIfProvided(metadataJson, nameof(metadataJson));
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
    }

    public void RegisterTelemetrySnapshot(
        string payloadJson,
        DateTime receivedAtUtc,
        DateTime occurredAtUtc,
        long? sequence)
    {
        ValidateJsonIfProvided(payloadJson, nameof(payloadJson));

        var normalizedReceivedAtUtc = EnsureUtc(receivedAtUtc, nameof(receivedAtUtc));
        var normalizedOccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (LastContactAtUtc is null || normalizedReceivedAtUtc >= LastContactAtUtc.Value)
            LastContactAtUtc = normalizedReceivedAtUtc;

        if (LastTelemetryReceivedAtUtc is not null && normalizedReceivedAtUtc < LastTelemetryReceivedAtUtc.Value)
            return;

        LastTelemetryReceivedAtUtc = normalizedReceivedAtUtc;
        LastTelemetryOccurredAtUtc = normalizedOccurredAtUtc;
        LastTelemetrySequence = sequence;
        LastTelemetryPayloadJson = payloadJson.Trim();
    }

    public DeviceOperationalStatus GetOperationalStatus(DateTime nowUtc, TimeSpan offlineAfter)
    {
        if (offlineAfter <= TimeSpan.Zero)
            throw new ArgumentException("Offline interval must be greater than zero.", nameof(offlineAfter));

        var normalizedNowUtc = EnsureUtc(nowUtc, nameof(nowUtc));

        if (LastContactAtUtc is null)
            return DeviceOperationalStatus.Unknown;

        return normalizedNowUtc - LastContactAtUtc.Value <= offlineAfter
            ? DeviceOperationalStatus.Online
            : DeviceOperationalStatus.Offline;
    }

    public static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId)
            ? DefaultTenantId
            : tenantId.Trim();
    }

    private static void ValidateJsonIfProvided(string? json, string paramName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("MetadataJson must be a valid JSON document.", paramName, ex);
        }
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
