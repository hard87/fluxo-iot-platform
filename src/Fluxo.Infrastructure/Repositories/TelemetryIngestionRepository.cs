using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fluxo.Infrastructure.Repositories;

public class TelemetryIngestionRepository : ITelemetryIngestionRepository
{
    private readonly FluxoDbContext _context;

    public TelemetryIngestionRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task<TelemetryIngestionWriteResult> AddAsync(
        TelemetryIngestionRecord telemetry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_context.Entry(telemetry).State == EntityState.Detached)
                await _context.TelemetryIngestionRecords.AddAsync(telemetry, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return TelemetryIngestionWriteResult.Persisted;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _context.Entry(telemetry).State = EntityState.Detached;
            return TelemetryIngestionWriteResult.Duplicate;
        }
        catch
        {
            _context.Entry(telemetry).State = EntityState.Detached;
            throw;
        }
    }

    public async Task<IReadOnlyList<TelemetryIngestionRecord>> GetByWorkspaceAndDeviceAsync(
        Guid workspaceId,
        string deviceId,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(page - 1, 0) * Math.Max(pageSize, 1);

        return await _context.TelemetryIngestionRecords
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.DeviceId == deviceId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.ReceivedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountByTenantWorkspaceAsync(
        string tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);

        return await _context.TelemetryIngestionRecords
            .AsNoTracking()
            .LongCountAsync(
                x => x.TenantId == normalizedTenantId && x.WorkspaceId == workspaceId,
                cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgres &&
               postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
