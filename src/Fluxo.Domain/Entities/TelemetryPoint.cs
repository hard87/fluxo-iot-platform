namespace Fluxo.Domain.Entities;

public sealed class TelemetryPoint
{
    private TelemetryPoint() { }
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid WorkspaceId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public Guid MetricDefinitionId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public double? NumericValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public string? TextValue { get; private set; }
    public Guid IngestionRecordId { get; private set; }

    public TelemetryPoint(string tenantId, Guid workspaceId, string deviceId, Guid definitionId,
        DateTime occurredAtUtc, Guid ingestionRecordId, double? numeric = null, bool? boolean = null, string? text = null)
    {
        if ((numeric.HasValue ? 1 : 0) + (boolean.HasValue ? 1 : 0) + (text is not null ? 1 : 0) != 1)
            throw new ArgumentException("Exactly one telemetry value is required.");
        Id = Guid.NewGuid(); TenantId = tenantId; WorkspaceId = workspaceId; DeviceId = deviceId;
        MetricDefinitionId = definitionId; OccurredAtUtc = occurredAtUtc; IngestionRecordId = ingestionRecordId;
        NumericValue = numeric; BooleanValue = boolean; TextValue = text;
    }
}

