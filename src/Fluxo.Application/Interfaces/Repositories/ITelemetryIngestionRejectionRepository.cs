using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface ITelemetryIngestionRejectionRepository
{
    Task AddAsync(
        TelemetryIngestionRejectionRecord rejection,
        CancellationToken cancellationToken = default);
}
