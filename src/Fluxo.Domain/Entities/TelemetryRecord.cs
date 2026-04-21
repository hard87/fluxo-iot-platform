using System.Text.Json;

namespace Fluxo.Domain.Entities;

public sealed class TelemetryRecord
{
    private TelemetryRecord()
    {
    }

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime IngestedAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    public TelemetryRecord(Guid deviceId, string payloadJson, DateTime? occurredAtUtc = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson is required.", nameof(payloadJson));

        EnsureJsonIsValid(payloadJson);

        Id = Guid.NewGuid();
        DeviceId = deviceId;
        OccurredAtUtc = EnsureUtc(occurredAtUtc ?? DateTime.UtcNow, nameof(occurredAtUtc));
        IngestedAtUtc = DateTime.UtcNow;
        PayloadJson = payloadJson.Trim();
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
