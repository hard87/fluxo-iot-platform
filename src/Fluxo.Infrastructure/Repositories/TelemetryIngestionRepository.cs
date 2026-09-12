using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Fluxo.Domain.Models;
using Fluxo.Domain.Exceptions;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Telemetry;
using Microsoft.Extensions.Options;

namespace Fluxo.Infrastructure.Repositories;

public class TelemetryIngestionRepository : ITelemetryIngestionRepository
{
    private readonly FluxoDbContext _context;

    private readonly ITelemetryPointWriter _pointWriter;
    private readonly IMetricDefinitionCache _definitionCache;
    private readonly TelemetryCatalogOptions _catalogOptions;
    public TelemetryIngestionRepository(FluxoDbContext context, ITelemetryPointWriter? pointWriter = null,
        IMetricDefinitionCache? definitionCache = null, IOptions<TelemetryCatalogOptions>? catalogOptions = null)
    {
        _context = context;
        _pointWriter = pointWriter ?? new EfTelemetryPointWriter(context);
        _definitionCache = definitionCache ?? new MetricDefinitionCache();
        _catalogOptions = catalogOptions?.Value ?? new TelemetryCatalogOptions();
    }

    public async Task<TelemetryIngestionWriteResult> AddWithPointsAsync(TelemetryIngestionRecord telemetry,
        IReadOnlyList<TelemetryMetricValue> metrics, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await Alerts.AlertTransactions.LockIngestionAsync(_context, telemetry, cancellationToken);
            await _context.TelemetryIngestionRecords.AddAsync(telemetry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var points = new List<TelemetryPoint>(metrics.Count);
            var cacheUpdates = new List<(string Key, MetricDefinitionCacheEntry Entry)>();
            foreach (var metric in metrics)
            {
                if (_definitionCache.TryGet(telemetry.WorkspaceId, metric.Key, out var cached))
                {
                    if (cached.ValueType != metric.ValueType) throw new MetricTypeMismatchException(metric.Key);
                    if (cached.Status != MetricDefinitionStatus.Ignored)
                        points.Add(new TelemetryPoint(telemetry.TenantId, telemetry.WorkspaceId, telemetry.DeviceId,
                            cached.Id, telemetry.OccurredAtUtc, telemetry.Id, metric.NumericValue, metric.BooleanValue, metric.TextValue));
                    continue;
                }
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({telemetry.WorkspaceId + ":" + telemetry.DeviceId}))", cancellationToken);
                var definition = await _context.MetricDefinitions.SingleOrDefaultAsync(
                    x => x.WorkspaceId == telemetry.WorkspaceId && x.MetricKey == metric.Key, cancellationToken);
                if (definition is null)
                {
                    var discoveries = await _context.Database.SqlQuery<int>($"""
                        SELECT COUNT(*)::int AS "Value" FROM metric_definition_discovery_audits
                        WHERE "WorkspaceId" = {telemetry.WorkspaceId} AND "DeviceId" = {telemetry.DeviceId}
                          AND "DiscoveredAtUtc" > CURRENT_TIMESTAMP - INTERVAL '1 hour'
                        """).SingleAsync(cancellationToken);
                    if (discoveries >= _catalogOptions.MaxNewMetricKeysPerDevicePerHour)
                        throw new MetricCardinalityGuardException(metric.Key);
                    var workspaceDefinitions = await _context.MetricDefinitions.CountAsync(
                        x => x.WorkspaceId == telemetry.WorkspaceId, cancellationToken);
                    if (workspaceDefinitions >= _catalogOptions.MaxMetricDefinitionsPerWorkspace)
                        throw new MetricWorkspaceLimitException(metric.Key, _catalogOptions.MaxMetricDefinitionsPerWorkspace);
                    definition = new MetricDefinition(telemetry.WorkspaceId, telemetry.TenantId, metric.Key, metric.ValueType, DateTime.UtcNow);
                    var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO metric_definitions ("Id", "WorkspaceId", "TenantId", "MetricKey", "DisplayName", "ValueType",
                          "Status", "IsQueryable", "IsAlertable", "CreatedAtUtc", "UpdatedAtUtc")
                        VALUES ({definition.Id}, {definition.WorkspaceId}, {definition.TenantId}, {definition.MetricKey},
                          {definition.DisplayName}, {definition.ValueType.ToString()}, {definition.Status.ToString()},
                          {definition.IsQueryable}, {definition.IsAlertable}, {definition.CreatedAtUtc}, {definition.UpdatedAtUtc})
                        ON CONFLICT ("WorkspaceId", "MetricKey") DO NOTHING
                        """, cancellationToken);
                    if (inserted == 1)
                    {
                        await _context.MetricDefinitionDiscoveryAudits.AddAsync(new MetricDefinitionDiscoveryAudit(
                            telemetry.WorkspaceId, telemetry.TenantId, telemetry.DeviceId, metric.Key, telemetry.Id), cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        definition = await _context.MetricDefinitions.AsNoTracking().SingleAsync(
                            x => x.WorkspaceId == telemetry.WorkspaceId && x.MetricKey == metric.Key, cancellationToken);
                    }
                }
                if (definition.ValueType != metric.ValueType) throw new MetricTypeMismatchException(metric.Key);
                cacheUpdates.Add((metric.Key, new(definition.Id, definition.ValueType, definition.Status)));
                if (definition.Status == MetricDefinitionStatus.Ignored) continue;
                points.Add(new TelemetryPoint(telemetry.TenantId, telemetry.WorkspaceId, telemetry.DeviceId,
                    definition.Id, telemetry.OccurredAtUtc, telemetry.Id, metric.NumericValue, metric.BooleanValue, metric.TextValue));
            }
            await _pointWriter.WriteAsync(points, cancellationToken);
            await Alerts.AlertTransactions.EnqueueAsync(_context, telemetry, points, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            foreach (var update in cacheUpdates) _definitionCache.Set(telemetry.WorkspaceId, update.Key, update.Entry);
            return TelemetryIngestionWriteResult.Persisted;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        { await transaction.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); return TelemetryIngestionWriteResult.Duplicate; }
        catch { await transaction.RollbackAsync(cancellationToken); _context.ChangeTracker.Clear(); throw; }
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
               postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
               postgres.ConstraintName == "UX_telemetry_ingestion_records_tenant_workspace_device_sequence";
    }
}
