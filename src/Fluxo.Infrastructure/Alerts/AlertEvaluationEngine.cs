using Fluxo.Application.Alerts;
using Fluxo.Domain.Alerts;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fluxo.Infrastructure.Alerts;

public sealed record AlertClaim(Guid WorkspaceId, Guid AttemptId, string DeviceIdentifier, Guid Token);
public sealed class LostAlertLeaseException() : Exception("Alert evaluation lease is no longer owned.");

// Optional deterministic transaction observer used by relational fault/barrier tests.
// No implementation is registered in application/worker DI.
public interface IAlertEvaluationProbe
{
    Task AtAsync(string stage, Guid attemptId, CancellationToken ct);
}

public sealed class AlertEvaluationEngine(FluxoDbContext db, IOptions<AlertEvaluationOptions> configured,
    IAlertEvaluationProbe? probe = null)
{
    private AlertEvaluationOptions Options => configured.Value;

    public async Task<AlertClaim?> ClaimAsync(string workerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerId) || workerId.Length > 120) throw new ArgumentException("Invalid worker id.");
        db.ChangeTracker.Clear();
        // This is discovery only. Eligibility is rechecked after catalog + device locks.
        var candidates = await db.Set<AlertEvaluationAttempt>().FromSqlRaw("""
            SELECT a.* FROM alert_evaluation_attempts a
            WHERE (a."Status"='Pending' OR (a."Status"='Failed' AND a."NextAttemptAtUtc" <= clock_timestamp())
                OR (a."Status"='Claimed' AND a."LeaseUntilUtc" <= clock_timestamp()))
              AND NOT EXISTS (SELECT 1 FROM alert_evaluation_attempts previous
                WHERE previous."WorkspaceId"=a."WorkspaceId" AND previous."DeviceIdentifier"=a."DeviceIdentifier"
                  AND previous."RuleId"=a."RuleId" AND previous."QueueOrder"<a."QueueOrder"
                  AND previous."Status" IN ('Pending','Claimed','Failed'))
            ORDER BY a."QueueOrder", a."Id" LIMIT 64
            """).AsNoTracking().ToListAsync(ct);
        foreach (var candidate in candidates)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AlertTransactions.CatalogAsync(db, candidate.WorkspaceId, false, ct);
            if (!await AlertTransactions.DeviceAsync(db, candidate.WorkspaceId, candidate.DeviceIdentifier, true, ct)) continue;
            var attempt = await db.Set<AlertEvaluationAttempt>().SingleAsync(x => x.Id == candidate.Id, ct);
            var now = await AlertTransactions.NowAsync(db, ct);
            if (!(attempt.Status == "Pending" || attempt.Status == "Failed" && attempt.NextAttemptAtUtc <= now ||
                attempt.Status == "Claimed" && attempt.LeaseUntilUtc <= now)) continue;
            if (await db.Set<AlertEvaluationAttempt>().AnyAsync(x => x.WorkspaceId == attempt.WorkspaceId &&
                x.DeviceIdentifier == attempt.DeviceIdentifier && x.RuleId == attempt.RuleId && x.QueueOrder < attempt.QueueOrder &&
                (x.Status == "Pending" || x.Status == "Claimed" || x.Status == "Failed"), ct)) continue;
            if (attempt.AttemptCount >= Options.MaxAttempts)
            {
                attempt.Status = "DeadLetter"; attempt.Reason = "LeaseRecoveryExhausted";
                attempt.LeaseToken = null; attempt.LeaseUntilUtc = null;
                await BreakContinuityAsync(attempt, ct);
                await AlertTransactions.CompleteParentAsync(db, attempt.WorkspaceId, attempt.WorkItemId, ct);
                await tx.CommitAsync(ct);
                continue;
            }
            attempt.Status = "Claimed"; attempt.AttemptCount++; attempt.LeaseToken = Guid.NewGuid();
            attempt.LeaseUntilUtc = now.AddSeconds(Options.WorkItemLeaseSeconds); attempt.ClaimedBy = workerId;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(attempt.WorkspaceId, attempt.Id, attempt.DeviceIdentifier, attempt.LeaseToken.Value);
        }
        return null;
    }

    public async Task EvaluateAsync(AlertClaim claim, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(claim, ct);
        var attempt = await OwnedAsync(claim, ct);
        var rule = await db.Set<AlertRule>().AsNoTracking().SingleAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Id == attempt.RuleId, ct);
        var revision = await db.Set<AlertRuleRevision>().AsNoTracking().SingleAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Id == attempt.RevisionId, ct);
        var work = await db.Set<AlertEvaluationWorkItem>().AsNoTracking().SingleAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Id == attempt.WorkItemId, ct);
        var record = await db.TelemetryIngestionRecords.AsNoTracking().SingleAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Id == work.IngestionRecordId, ct);
        string? skipped = null;
        if (rule.CurrentRevisionId != revision.Id || !revision.Enabled) skipped = "RevisionInactive";
        else if (record.OccurredAtUtc <= revision.ActivatedAtUtc) skipped = "BeforeActivation";
        else if (!await db.Workspaces.AnyAsync(x => x.Id == claim.WorkspaceId && x.IsActive, ct) ||
            !await db.Devices.AnyAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Identifier == claim.DeviceIdentifier && x.IsActive, ct)) skipped = "ScopeInactive";
        var point = await db.TelemetryPoints.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == claim.WorkspaceId &&
            x.IngestionRecordId == record.Id && x.MetricDefinitionId == revision.MetricDefinitionId, ct);
        if (point is null) skipped ??= "MetricUnavailable";
        var now = await AlertTransactions.NowAsync(db, ct);
        if (skipped is null)
        {
            if (revision.ValueType == "Numeric" && point!.NumericValue is null || revision.ValueType == "Boolean" && point!.BooleanValue is null)
                throw new InvalidOperationException("Alert metric type no longer matches its revision.");
            var state = await db.Set<AlertRuleState>().SingleOrDefaultAsync(x => x.WorkspaceId == claim.WorkspaceId &&
                x.RuleId == rule.Id && x.DeviceIdentifier == claim.DeviceIdentifier, ct);
            if (state is null)
            {
                state = new AlertRuleState { WorkspaceId = claim.WorkspaceId, RuleId = rule.Id,
                    RevisionId = revision.Id, DeviceIdentifier = claim.DeviceIdentifier };
                db.Add(state);
            }
            if (probe is not null) await probe.AtAsync("BeforeEvaluate", attempt.Id, ct);
            var result = AlertEvaluator.Observe(revision, state, record.OccurredAtUtc, record.Sequence ?? 0,
                record.Id, point!.NumericValue, point.BooleanValue);
            if (result == "Historical") skipped = "Historical";
            else if (result == "Firing")
            {
                var occurrence = new AlertEvent { WorkspaceId = claim.WorkspaceId, RuleId = rule.Id, RevisionId = revision.Id,
                    DeviceIdentifier = claim.DeviceIdentifier, TriggeredAtUtc = record.OccurredAtUtc };
                db.Add(occurrence); state.ActiveEventId = occurrence.Id;
                AlertTransactions.Transition(db, occurrence, "Firing", record.OccurredAtUtc, now, "ConditionSatisfied", record, point);
            }
            else if (result == "Resolved")
            {
                var occurrence = await db.Set<AlertEvent>().SingleAsync(x => x.WorkspaceId == claim.WorkspaceId && x.Id == state.ActiveEventId, ct);
                AlertTransactions.Transition(db, occurrence, "Resolved", record.OccurredAtUtc, now, "RecoveryObserved", record, point);
                state.ActiveEventId = null;
            }
        }
        await db.SaveChangesAsync(ct);
        if (probe is not null) await probe.AtAsync("AfterSaveBeforeCommit", attempt.Id, ct);
        var status = skipped is null ? "Completed" : "Skipped";
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE alert_evaluation_attempts SET "Status"={status}, "Reason"={skipped}, "LeaseToken"=NULL, "LeaseUntilUtc"=NULL
            WHERE "WorkspaceId"={claim.WorkspaceId} AND "Id"={claim.AttemptId} AND "Status"='Claimed'
              AND "LeaseToken"={claim.Token} AND "LeaseUntilUtc">clock_timestamp()
            """, ct);
        if (changed != 1) throw new LostAlertLeaseException();
        db.Entry(attempt).State = EntityState.Detached;
        await AlertTransactions.CompleteParentAsync(db, claim.WorkspaceId, work.Id, ct);
        await tx.CommitAsync(ct);
    }

    public async Task FailAsync(AlertClaim claim, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(claim, ct);
        var attempt = await OwnedAsync(claim, ct);
        var terminal = attempt.AttemptCount >= Options.MaxAttempts;
        var status = terminal ? "DeadLetter" : "Failed";
        var now = await AlertTransactions.NowAsync(db, ct);
        var next = now.AddSeconds(Math.Min(300, Options.RetryBaseSeconds * Math.Pow(2, Math.Min(attempt.AttemptCount - 1, 10))));
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE alert_evaluation_attempts SET "Status"={status}, "Reason"='EvaluationFailed', "NextAttemptAtUtc"={next},
                "LeaseToken"=NULL, "LeaseUntilUtc"=NULL
            WHERE "WorkspaceId"={claim.WorkspaceId} AND "Id"={claim.AttemptId} AND "Status"='Claimed'
                AND "LeaseToken"={claim.Token} AND "LeaseUntilUtc">clock_timestamp()
            """, ct);
        if (changed != 1) throw new LostAlertLeaseException();
        db.Entry(attempt).State = EntityState.Detached;
        if (terminal) await BreakContinuityAsync(attempt, ct);
        await AlertTransactions.CompleteParentAsync(db, claim.WorkspaceId, attempt.WorkItemId, ct);
        await tx.CommitAsync(ct);
    }

    private async Task LockAsync(AlertClaim claim, CancellationToken ct)
    {
        await AlertTransactions.CatalogAsync(db, claim.WorkspaceId, false, ct);
        if (!await AlertTransactions.DeviceAsync(db, claim.WorkspaceId, claim.DeviceIdentifier, false, ct)) throw new LostAlertLeaseException();
    }
    private async Task<AlertEvaluationAttempt> OwnedAsync(AlertClaim claim, CancellationToken ct)
    {
        var attempt = await db.Set<AlertEvaluationAttempt>().SingleOrDefaultAsync(x => x.WorkspaceId == claim.WorkspaceId &&
            x.Id == claim.AttemptId && x.DeviceIdentifier == claim.DeviceIdentifier, ct);
        var now = await AlertTransactions.NowAsync(db, ct);
        if (attempt is null || attempt.Status != "Claimed" || attempt.LeaseToken != claim.Token || attempt.LeaseUntilUtc <= now)
            throw new LostAlertLeaseException();
        return attempt;
    }
    private async Task BreakContinuityAsync(AlertEvaluationAttempt attempt, CancellationToken ct)
    {
        var state = await db.Set<AlertRuleState>().SingleOrDefaultAsync(x => x.WorkspaceId == attempt.WorkspaceId &&
            x.RuleId == attempt.RuleId && x.DeviceIdentifier == attempt.DeviceIdentifier, ct);
        if (state is not null) state.FirstViolationAtUtc = null;
    }
}
