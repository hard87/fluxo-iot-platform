using Fluxo.Domain.Alerts;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Alerts;

internal static class AlertTransactions
{
    public static Task CatalogAsync(FluxoDbContext db, Guid workspace, bool exclusive, CancellationToken ct) => exclusive
        ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"fluxo:alerts:" + workspace}, 0))", ct)
        : db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(hashtextextended({"fluxo:alerts:" + workspace}, 0))", ct);

    public static Task<DateTime> NowAsync(FluxoDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    public static async Task<bool> DeviceAsync(FluxoDbContext db, Guid workspace, string device, bool skipLocked, CancellationToken ct)
    {
        var rows = skipLocked
            ? await db.Set<AlertDeviceCoordination>().FromSqlInterpolated($"""
                SELECT * FROM alert_device_coordination WHERE "WorkspaceId"={workspace} AND "DeviceIdentifier"={device} FOR UPDATE SKIP LOCKED
                """).AsNoTracking().ToListAsync(ct)
            : await db.Set<AlertDeviceCoordination>().FromSqlInterpolated($"""
                SELECT * FROM alert_device_coordination WHERE "WorkspaceId"={workspace} AND "DeviceIdentifier"={device} FOR UPDATE
                """).AsNoTracking().ToListAsync(ct);
        return rows.Count == 1;
    }

    public static async Task LockIngestionAsync(FluxoDbContext db, TelemetryIngestionRecord record, CancellationToken ct)
    {
        await CatalogAsync(db, record.WorkspaceId, false, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO alert_device_coordination ("WorkspaceId", "DeviceIdentifier", "NextOrder")
            VALUES ({record.WorkspaceId}, {record.DeviceId}, 0) ON CONFLICT DO NOTHING
            """, ct);
        await DeviceAsync(db, record.WorkspaceId, record.DeviceId, false, ct);
    }

    public static async Task EnqueueAsync(FluxoDbContext db, TelemetryIngestionRecord record,
        IReadOnlyCollection<TelemetryPoint> points, CancellationToken ct)
    {
        var order = (await db.Database.SqlQuery<long>($"""
            UPDATE alert_device_coordination SET "NextOrder"="NextOrder"+1
            WHERE "WorkspaceId"={record.WorkspaceId} AND "DeviceIdentifier"={record.DeviceId} RETURNING "NextOrder" AS "Value"
            """).ToListAsync(ct)).Single();
        var metricIds = points.Select(x => x.MetricDefinitionId).Distinct().ToArray();
        var revisions = await (from r in db.Set<AlertRule>()
            join v in db.Set<AlertRuleRevision>() on r.CurrentRevisionId equals v.Id
            where r.WorkspaceId == record.WorkspaceId && v.Enabled && metricIds.Contains(v.MetricDefinitionId) &&
                (v.DeviceIdentifier == null || v.DeviceIdentifier == record.DeviceId)
            select v).AsNoTracking().ToListAsync(ct);
        var work = new AlertEvaluationWorkItem
        {
            WorkspaceId = record.WorkspaceId, DeviceIdentifier = record.DeviceId, IngestionRecordId = record.Id,
            QueueOrder = order, CreatedAtUtc = await NowAsync(db, ct), Status = revisions.Count == 0 ? "Completed" : "Pending"
        };
        db.Add(work);
        foreach (var revision in revisions)
            db.Add(new AlertEvaluationAttempt
            {
                WorkspaceId = record.WorkspaceId, DeviceIdentifier = record.DeviceId, WorkItemId = work.Id,
                RuleId = revision.RuleId, RevisionId = revision.Id, QueueOrder = order
            });
    }

    public static void Transition(FluxoDbContext db, AlertEvent occurrence, string kind, DateTime occurred,
        DateTime now, string reason, TelemetryIngestionRecord? record = null, TelemetryPoint? point = null)
    {
        occurrence.Status = kind;
        var transition = new AlertEventTransition
        {
            WorkspaceId = occurrence.WorkspaceId, EventId = occurrence.Id, Ordinal = ++occurrence.Ordinal,
            Kind = kind, OccurredAtUtc = occurred, RecordedAtUtc = now, Reason = reason,
            ReceivedAtUtc = record?.ReceivedAtUtc, IngestionRecordId = record?.Id,
            NumericValue = point?.NumericValue, BooleanValue = point?.BooleanValue
        };
        db.Add(transition);
        db.Add(new AlertDeliveryIntent { WorkspaceId = occurrence.WorkspaceId, TransitionId = transition.Id, CreatedAtUtc = now });
    }

    public static async Task CompleteParentAsync(FluxoDbContext db, Guid workspace, Guid workId, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var statuses = await db.Set<AlertEvaluationAttempt>().Where(x => x.WorkspaceId == workspace && x.WorkItemId == workId)
            .Select(x => x.Status).ToListAsync(ct);
        var work = await db.Set<AlertEvaluationWorkItem>().SingleAsync(x => x.WorkspaceId == workspace && x.Id == workId, ct);
        work.Status = statuses.Any(x => x is "Pending" or "Claimed" or "Failed") ? "Pending"
            : statuses.Contains("DeadLetter") ? "CompletedWithFailures" : "Completed";
        await db.SaveChangesAsync(ct);
    }
}
