namespace Fluxo.Worker.Ingestion.Models;
using System.Text.Json;

public sealed class IncomingTelemetryMessage
{
    public string? SchemaVersion { get; init; }
    public string? TenantId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? DeviceId { get; init; }
    public string? MessageType { get; init; }
    public string? TimestampUtc { get; init; }
    public long? Sequence { get; init; }
    public string? FirmwareVersion { get; init; }
    public IncomingTelemetryMetrics? Metrics { get; init; }
}

public sealed class IncomingTelemetryV2Message
{
    public int SchemaVersion { get; init; }
    public long? Sequence { get; init; }
    public string? OccurredAtUtc { get; init; }
    public Dictionary<string, JsonElement>? Metrics { get; init; }
}

public sealed class IncomingTelemetryMetrics
{
    public double? Temperature { get; init; }
    public double? Humidity { get; init; }
    public double? Battery { get; init; }
    public int? Rssi { get; init; }
    public long? UptimeSec { get; init; }
}
