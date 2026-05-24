using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;

namespace Fluxo.Infrastructure.Repositories;

public class TelemetryIngestionRejectionRepository : ITelemetryIngestionRejectionRepository
{
    private readonly FluxoDbContext _context;

    public TelemetryIngestionRejectionRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        TelemetryIngestionRejectionRecord rejection,
        CancellationToken cancellationToken = default)
    {
        await _context.TelemetryIngestionRejectionRecords.AddAsync(rejection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
