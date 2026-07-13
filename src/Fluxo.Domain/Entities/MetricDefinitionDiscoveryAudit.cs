namespace Fluxo.Domain.Entities;

public sealed class MetricDefinitionDiscoveryAudit
{
    private MetricDefinitionDiscoveryAudit() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public string MetricKey { get; private set; } = string.Empty;
    public DateTime DiscoveredAtUtc { get; private set; }
    public Guid IngestionRecordId { get; private set; }
    public MetricDefinitionDiscoveryAudit(Guid workspaceId, string tenantId, string deviceId, string metricKey, Guid ingestionRecordId)
    { Id = Guid.NewGuid(); WorkspaceId = workspaceId; TenantId = tenantId; DeviceId = deviceId; MetricKey = metricKey; IngestionRecordId = ingestionRecordId; }
}

