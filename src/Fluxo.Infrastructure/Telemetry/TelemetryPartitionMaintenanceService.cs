using System.Diagnostics.Metrics;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxo.Infrastructure.Telemetry;

public sealed class TelemetryPartitionState
{
    public int PartitionsAhead { get; internal set; }
    public DateTime? LastSuccessUtc { get; internal set; }
    public DateTime? LastFailureUtc { get; internal set; }
    public bool NextMonthExists { get; internal set; }
}

public sealed class TelemetryPartitionMaintenanceService(IServiceScopeFactory scopeFactory,
    TelemetryPartitionState state, ILogger<TelemetryPartitionMaintenanceService> logger) : BackgroundService
{
    private static readonly Meter Meter = new("Fluxo.Telemetry.Partitions", "1.0.0");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Meter.CreateObservableGauge("partitions_ahead", () => state.PartitionsAhead);
        Meter.CreateObservableGauge("last_partition_maintenance_success", () => state.LastSuccessUtc?.Ticks ?? 0);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await MaintainAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Telemetry partition maintenance failed."); }
            var now = DateTime.UtcNow; var next = now.Date.AddDays(1).AddHours(3);
            await Task.Delay(next - now, stoppingToken);
        }
    }

    public async Task MaintainAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fluxo:telemetry-partitions'))", cancellationToken);
        try
        {
            var current = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i <= 6; i++)
            {
                var from = current.AddMonths(i); var to = from.AddMonths(1); var name = $"telemetry_points_{from:yyyy_MM}";
                var sql = $"CREATE TABLE IF NOT EXISTS {name} PARTITION OF telemetry_points FOR VALUES FROM ('{from:yyyy-MM-dd}') TO ('{to:yyyy-MM-dd}')";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            await RefreshStateAsync(cancellationToken); state.LastSuccessUtc = DateTime.UtcNow;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            state.LastFailureUtc = DateTime.UtcNow;
            throw;
        }
    }

    public async Task RefreshStateAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        var current = DateTime.UtcNow.ToString("yyyy_MM"); var next = DateTime.UtcNow.AddMonths(1).ToString("yyyy_MM");
        state.PartitionsAhead = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM pg_inherits i JOIN pg_class c ON c.oid=i.inhrelid
            JOIN pg_class p ON p.oid=i.inhparent WHERE p.relname='telemetry_points'
              AND c.relname > {"telemetry_points_" + current}
            """).SingleAsync(cancellationToken);
        state.NextMonthExists = await db.Database.SqlQuery<bool>($"SELECT to_regclass({"telemetry_points_" + next}) IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);
    }
}
