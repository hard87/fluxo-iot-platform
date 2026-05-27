using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryIngestionRejectionRepository
{
    Task AddAsync(
        TelemetryIngestionRejectionRecord rejection,
        CancellationToken cancellationToken = default);

    Task<long> CountByTenantWorkspaceAsync(
        string tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
