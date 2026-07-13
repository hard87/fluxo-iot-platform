using System.Collections.Concurrent;
using Fluxo.Application.Interfaces.Repositories;
namespace Fluxo.Infrastructure.Repositories;
public sealed class MetricDefinitionCache : IMetricDefinitionCache
{
    private readonly ConcurrentDictionary<(Guid, string), MetricDefinitionCacheEntry> _entries = new();
    public bool TryGet(Guid workspaceId, string metricKey, out MetricDefinitionCacheEntry entry) => _entries.TryGetValue((workspaceId, metricKey), out entry!);
    public void Set(Guid workspaceId, string metricKey, MetricDefinitionCacheEntry entry) => _entries[(workspaceId, metricKey)] = entry;
    public void Invalidate(Guid workspaceId, string metricKey) => _entries.TryRemove((workspaceId, metricKey), out _);
}
