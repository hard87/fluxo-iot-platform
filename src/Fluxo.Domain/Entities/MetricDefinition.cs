using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class MetricDefinition
{
    private MetricDefinition() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string MetricKey { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public MetricValueType ValueType { get; private set; }
    public string? SemanticType { get; private set; }
    public string? CanonicalUnit { get; private set; }
    public int? ExpectedIntervalSec { get; private set; }
    public double? MinExpectedValue { get; private set; }
    public double? MaxExpectedValue { get; private set; }
    public MetricDefinitionStatus Status { get; private set; }
    public bool IsQueryable { get; private set; }
    public bool IsAlertable { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public MetricDefinition(Guid workspaceId, string tenantId, string metricKey, MetricValueType valueType, DateTime nowUtc)
    {
        Id = Guid.NewGuid(); WorkspaceId = workspaceId; TenantId = tenantId;
        MetricKey = metricKey; DisplayName = metricKey; ValueType = valueType;
        Status = MetricDefinitionStatus.Discovered; IsQueryable = true; IsAlertable = true;
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
}

