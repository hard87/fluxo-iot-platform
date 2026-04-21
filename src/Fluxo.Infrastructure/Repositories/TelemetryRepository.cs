using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly FluxoDbContext _context;

    public TelemetryRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TelemetryRecord telemetry, CancellationToken cancellationToken = default)
    {
        await _context.TelemetryRecords.AddAsync(telemetry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetryRecord>> GetByDeviceIdAsync(
        Guid deviceId,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;

        return await _context.TelemetryRecords
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.IngestedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
