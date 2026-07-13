using System.Text.Json;

namespace Fluxo.Domain.Entities;

public sealed class TelemetryIngestionRecord
{
    private TelemetryIngestionRecord()
    {
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid WorkspaceId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string MessageType { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public string SchemaVersion { get; private set; } = string.Empty;
    public string? FirmwareVersion { get; private set; }
    public long? Sequence { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public double? Temperature { get; private set; }
    public double? Humidity { get; private set; }
    public double? Battery { get; private set; }
    public int? Rssi { get; private set; }
    public long? UptimeSec { get; private set; }

    public TelemetryIngestionRecord(
        string tenantId,
        Guid workspaceId,
        string deviceId,
        string messageType,
        string topic,
        string schemaVersion,
        DateTime occurredAtUtc,
        DateTime receivedAtUtc,
        string payloadJson,
        string? firmwareVersion = null,
        long? sequence = null,
        double? temperature = null,
        double? humidity = null,
        double? battery = null,
        int? rssi = null,
        long? uptimeSec = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("MessageType is required.", nameof(messageType));

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));

        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new ArgumentException("SchemaVersion is required.", nameof(schemaVersion));

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson is required.", nameof(payloadJson));

        EnsureJsonIsValid(payloadJson);

        Id = Guid.NewGuid();
        TenantId = tenantId.Trim();
        WorkspaceId = workspaceId;
        DeviceId = deviceId.Trim();
        MessageType = messageType.Trim();
        Topic = topic.Trim();
        SchemaVersion = schemaVersion.Trim();
        FirmwareVersion = string.IsNullOrWhiteSpace(firmwareVersion) ? null : firmwareVersion.Trim();
        Sequence = sequence;
        OccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        ReceivedAtUtc = EnsureUtc(receivedAtUtc, nameof(receivedAtUtc));
        PayloadJson = payloadJson.Trim();
        Temperature = temperature;
        Humidity = humidity;
        Battery = battery;
        Rssi = rssi;
        UptimeSec = uptimeSec;
    }

    private static void EnsureJsonIsValid(string value)
    {
        try
        {
            _ = JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("PayloadJson must be a valid JSON document.", nameof(value), ex);
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
