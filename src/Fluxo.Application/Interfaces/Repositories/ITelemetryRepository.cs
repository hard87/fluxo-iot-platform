using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryRepository
{
    Task AddAsync(TelemetryRecord telemetry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetryRecord>> GetByDeviceIdAsync(
        Guid deviceId,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);
}
