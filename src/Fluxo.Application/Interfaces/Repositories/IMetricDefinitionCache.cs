using Fluxo.Domain.Enums;
namespace Fluxo.Application.Interfaces.Repositories;
public sealed record MetricDefinitionCacheEntry(Guid Id, MetricValueType ValueType, MetricDefinitionStatus Status);
public interface IMetricDefinitionCache
{
    bool TryGet(Guid workspaceId, string metricKey, out MetricDefinitionCacheEntry entry);
    void Set(Guid workspaceId, string metricKey, MetricDefinitionCacheEntry entry);
    void Invalidate(Guid workspaceId, string metricKey);
}
