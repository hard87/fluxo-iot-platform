using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    public async Task<long> CountByTenantWorkspaceAsync(
        string tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);

        return await _context.TelemetryIngestionRejectionRecords
            .AsNoTracking()
            .LongCountAsync(
                x => x.TenantId == normalizedTenantId && x.WorkspaceId == workspaceId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetryIngestionRejectionRecord>> GetReprocessableBatchAsync(
        IReadOnlyCollection<TelemetryIngestionFailureType> errorTypes,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.TelemetryIngestionRejectionRecords
            .Where(x =>
                x.SourceRejectionId == null &&
                !x.Reprocessed &&
                x.ReprocessAttempts < maxAttempts &&
                errorTypes.Contains(x.ErrorType))
            .OrderBy(x => x.ReceivedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task RecordReprocessAttemptAsync(
        Guid rejectionId,
        DateTime attemptedAtUtc,
        bool resolved,
        CancellationToken cancellationToken = default)
    {
        var rejection = await _context.TelemetryIngestionRejectionRecords
            .FirstOrDefaultAsync(x => x.Id == rejectionId, cancellationToken);

        if (rejection is null)
            return;

        rejection.RecordReprocessAttempt(attemptedAtUtc, resolved);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
