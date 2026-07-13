using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Models;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryIngestionRepository
{
    Task<TelemetryIngestionWriteResult> AddAsync(
        TelemetryIngestionRecord telemetry,
        CancellationToken cancellationToken = default);
    Task<TelemetryIngestionWriteResult> AddWithPointsAsync(TelemetryIngestionRecord telemetry,
        IReadOnlyList<TelemetryMetricValue> metrics, CancellationToken cancellationToken = default)
        => AddAsync(telemetry, cancellationToken);

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
