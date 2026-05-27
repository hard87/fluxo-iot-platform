using Fluxo.Application.Models;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryIngestionRepository
{
    Task<TelemetryIngestionWriteResult> AddAsync(
        TelemetryIngestionRecord telemetry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelemetryIngestionRecord>> GetByWorkspaceAndDeviceAsync(
        Guid workspaceId,
        string deviceId,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<long> CountByTenantWorkspaceAsync(
        string tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
