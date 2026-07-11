using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;

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

    Task<IReadOnlyList<TelemetryIngestionRejectionRecord>> GetReprocessableBatchAsync(
        IReadOnlyCollection<TelemetryIngestionFailureType> errorTypes,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task RecordReprocessAttemptAsync(
        Guid rejectionId,
        DateTime attemptedAtUtc,
        bool resolved,
        CancellationToken cancellationToken = default);
}
