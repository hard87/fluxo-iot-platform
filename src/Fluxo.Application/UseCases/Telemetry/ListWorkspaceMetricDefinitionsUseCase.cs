using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Telemetry;

public sealed class ListWorkspaceMetricDefinitionsUseCase(GetAuthorizedWorkspaceUseCase authorize, ITelemetryQueryRepository repository)
{
    public async Task<IReadOnlyList<MetricDefinitionResponse>> ExecuteAsync(Guid userId, Guid workspaceId, CancellationToken ct)
    {
        var workspace = await authorize.ExecuteAsync(userId, workspaceId, WorkspaceMembershipRole.Viewer, ct);
        var rows = await repository.ListMetricDefinitionsAsync(workspace.Id, ct);
        return rows.Select(x => new MetricDefinitionResponse(x.Id, x.MetricKey, x.DisplayName, x.ValueType.ToString(),
            x.SemanticType, x.CanonicalUnit, x.Status.ToString(), x.IsQueryable)).ToArray();
    }
}
