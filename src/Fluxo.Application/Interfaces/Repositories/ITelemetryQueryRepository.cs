using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryQueryRepository
{
    Task<IReadOnlyList<TelemetryPointResponse>> QuerySeriesAsync(Guid workspaceId, Device device,
        MetricDefinition metric, DateTime fromUtc, DateTime toUtc, string aggregation, string? bucket,
        int rawLimit, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetricDefinition>> ListMetricDefinitionsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetricDefinition>> ResolveMetricDefinitionsAsync(Guid workspaceId,
        IReadOnlyCollection<string> metricKeys, CancellationToken cancellationToken);
}
