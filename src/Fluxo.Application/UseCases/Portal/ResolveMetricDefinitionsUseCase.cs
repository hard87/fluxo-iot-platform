using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Portal;

public sealed class ResolveMetricDefinitionsUseCase(ITelemetryQueryRepository repository)
{
    public async Task<IReadOnlyList<MetricDefinition>> ExecuteAsync(Guid workspaceId, IReadOnlyList<string> keys, CancellationToken ct)
    {
        var normalized = keys.Select(x => x.Trim()).ToArray();
        var found = await repository.ResolveMetricDefinitionsAsync(workspaceId, normalized, ct);
        var map = found.ToDictionary(x => x.MetricKey, StringComparer.Ordinal);
        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length || normalized.Any(x => !map.ContainsKey(x)))
            throw new NotFoundException("One or more metrics were not found in this workspace.");
        return normalized.Select(x => map[x]).ToArray();
    }
}
