using Fluxo.Application.Alerts;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Alerts;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fluxo.Infrastructure.Alerts;

public sealed class AlertManagement(FluxoDbContext db, GetAuthorizedWorkspaceUseCase authorize,
    IOptions<AlertEvaluationOptions> options) : IAlertManagement
{
    private async Task AuthorizeAsync(Guid user, Guid workspace, bool write, CancellationToken ct)
    {
        if (!await db.PlatformUsers.AsNoTracking().AnyAsync(x => x.Id == user && x.IsActive, ct))
            throw new UnauthorizedException("User is not active.");
        await authorize.ExecuteAsync(user, workspace, write ? WorkspaceMembershipRole.Admin : WorkspaceMembershipRole.Viewer, ct);
    }

    public async Task<AlertRuleRevision> SaveAsync(Guid userId, Guid workspaceId, Guid? ruleId,
        SaveAlertRuleRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, true, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AlertTransactions.CatalogAsync(db, workspaceId, true, ct);
        var rule = ruleId is { } id ? await db.Set<AlertRule>().SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct)
            ?? throw new NotFoundException("Alert rule not found.") : new AlertRule { WorkspaceId = workspaceId };
        if (ruleId.HasValue && request.ExpectedVersion != rule.Version) throw new ConflictException("Alert rule version changed.");
        if (!ruleId.HasValue && request.Enabled) throw new ValidationException("Create the rule disabled before activating it.");
        var metric = await db.MetricDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == request.MetricDefinitionId, ct)
            ?? throw new NotFoundException("Metric definition not found.");
        if (!metric.IsAlertable || metric.Status == MetricDefinitionStatus.Ignored || metric.ValueType == MetricValueType.Text)
            throw new ValidationException("Metric is not eligible for alerts.");
        if (request.DeviceIdentifier is not null && !await db.Devices.AsNoTracking().AnyAsync(
            x => x.WorkspaceId == workspaceId && x.Identifier == request.DeviceIdentifier && x.IsActive, ct))
            throw new NotFoundException("Device not found.");
        Validate(request, metric.ValueType);
        var now = await AlertTransactions.NowAsync(db, ct);
        var revision = new AlertRuleRevision
        {
            WorkspaceId = workspaceId, RuleId = rule.Id, Version = rule.Version + 1, Name = request.Name.Trim(),
            MetricDefinitionId = metric.Id, DeviceIdentifier = request.DeviceIdentifier,
            ValueType = metric.ValueType.ToString(), Unit = metric.CanonicalUnit,
            ExpectedIntervalSeconds = metric.ExpectedIntervalSec ?? options.Value.DefaultExpectedIntervalSec,
            Operator = request.Operator, Threshold = request.Threshold, ThresholdHigh = request.ThresholdHigh,
            Hysteresis = request.Hysteresis, DurationSeconds = request.DurationSeconds, CooldownSeconds = request.CooldownSeconds,
            Severity = request.Severity, Enabled = request.Enabled, ActivatedAtUtc = now, CreatedAtUtc = now, AuthorId = userId
        };
        if (!ruleId.HasValue) { db.Add(rule); await db.SaveChangesAsync(ct); }
        else
        {
            var events = await db.Set<AlertEvent>().Where(x => x.WorkspaceId == workspaceId && x.RuleId == rule.Id && x.Status == "Firing").ToListAsync(ct);
            foreach (var occurrence in events)
                AlertTransactions.Transition(db, occurrence, "Closed", now, now, request.Enabled ? "RuleRevised" : "RuleDisabled");
            var attempts = await db.Set<AlertEvaluationAttempt>().Where(x => x.WorkspaceId == workspaceId && x.RuleId == rule.Id &&
                (x.Status == "Pending" || x.Status == "Claimed" || x.Status == "Failed")).ToListAsync(ct);
            foreach (var attempt in attempts)
            {
                attempt.Status = "Skipped"; attempt.Reason = "RevisionReplaced";
                attempt.LeaseToken = null; attempt.LeaseUntilUtc = null;
            }
            db.RemoveRange(await db.Set<AlertRuleState>().Where(x => x.WorkspaceId == workspaceId && x.RuleId == rule.Id).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
            foreach (var work in attempts.Select(x => x.WorkItemId).Distinct())
                await AlertTransactions.CompleteParentAsync(db, workspaceId, work, ct);
        }
        db.Add(revision);
        await db.SaveChangesAsync(ct);
        rule.CurrentRevisionId = revision.Id; rule.Version = revision.Version;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return revision;
    }

    public static void Validate(SaveAlertRuleRequest r, MetricValueType type)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 120) throw new ValidationException("Name must contain 1 to 120 characters.");
        if (r.DurationSeconds < 0 || r.CooldownSeconds < 0 || !double.IsFinite(r.Hysteresis) || r.Hysteresis < 0 ||
            r.Threshold is { } t && !double.IsFinite(t) || r.ThresholdHigh is { } h && !double.IsFinite(h))
            throw new ValidationException("Invalid temporal or numeric parameters.");
        if (r.Severity is not ("Info" or "Warning" or "Critical")) throw new ValidationException("Invalid severity.");
        if (type == MetricValueType.Boolean)
        {
            if (r.Operator is not ("IsTrue" or "IsFalse") || r.Threshold.HasValue || r.ThresholdHigh.HasValue || r.Hysteresis != 0)
                throw new ValidationException("Boolean rules require IsTrue/IsFalse without thresholds or hysteresis.");
            return;
        }
        if (type != MetricValueType.Numeric || r.Operator is not ("GreaterThan" or "GreaterOrEqual" or "LessThan" or "LessOrEqual" or "InsideRange" or "OutsideRange") || !r.Threshold.HasValue)
            throw new ValidationException("Invalid numeric operator or missing threshold.");
        var range = r.Operator is "InsideRange" or "OutsideRange";
        if (range && (!r.ThresholdHigh.HasValue || r.Threshold > r.ThresholdHigh) || !range && r.ThresholdHigh.HasValue)
            throw new ValidationException("Invalid range.");
        if (r.Operator == "OutsideRange" && r.Hysteresis > 0 && r.Threshold + r.Hysteresis >= r.ThresholdHigh - r.Hysteresis)
            throw new ValidationException("Hysteresis leaves no recovery range.");
        if (!double.IsFinite(r.Threshold.Value + r.Hysteresis) || !double.IsFinite(r.Threshold.Value - r.Hysteresis) ||
            r.ThresholdHigh is { } high && (!double.IsFinite(high + r.Hysteresis) || !double.IsFinite(high - r.Hysteresis)))
            throw new ValidationException("Hysteresis exceeds numeric range.");
    }

    private static int Offset(int page) => page is < 1 or > 100000 ? throw new ValidationException("Invalid page.") : (page - 1) * 100;

    public async Task<IReadOnlyList<AlertRuleRevision>> RulesAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, false, ct);
        return await (from r in db.Set<AlertRule>() join v in db.Set<AlertRuleRevision>() on r.CurrentRevisionId equals v.Id
            where r.WorkspaceId == workspaceId select v).AsNoTracking().OrderBy(x => x.RuleId).Skip(Offset(page)).Take(100).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<AlertRuleRevision>> RevisionsAsync(Guid userId, Guid workspaceId, Guid ruleId, int page, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, false, ct);
        if (!await db.Set<AlertRule>().AnyAsync(x => x.WorkspaceId == workspaceId && x.Id == ruleId, ct)) throw new NotFoundException("Alert rule not found.");
        return await db.Set<AlertRuleRevision>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.RuleId == ruleId)
            .OrderBy(x => x.Version).Skip(Offset(page)).Take(100).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<AlertEvent>> EventsAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, false, ct);
        return await db.Set<AlertEvent>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId)
            .OrderByDescending(x => x.TriggeredAtUtc).ThenBy(x => x.Id).Skip(Offset(page)).Take(100).ToListAsync(ct);
    }
    public async Task<AlertHistory> HistoryAsync(Guid userId, Guid workspaceId, Guid eventId, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, false, ct);
        var occurrence = await db.Set<AlertEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == eventId, ct)
            ?? throw new NotFoundException("Alert occurrence not found.");
        var revision = await db.Set<AlertRuleRevision>().AsNoTracking().SingleAsync(x => x.WorkspaceId == workspaceId && x.Id == occurrence.RevisionId, ct);
        var transitions = await db.Set<AlertEventTransition>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.EventId == eventId).OrderBy(x => x.Ordinal).ToListAsync(ct);
        var ids = transitions.Select(x => x.Id).ToArray();
        return new(occurrence, revision, transitions,
            await db.Set<AlertAcknowledgement>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.EventId == eventId).ToListAsync(ct),
            await db.Set<AlertDeliveryIntent>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId && ids.Contains(x.TransitionId)).ToListAsync(ct));
    }
    public async Task<AlertAcknowledgement> AcknowledgeAsync(Guid userId, Guid workspaceId, Guid eventId, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, true, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var events = await db.Set<AlertEvent>().FromSqlInterpolated($"""
            SELECT * FROM alert_events WHERE "WorkspaceId"={workspaceId} AND "Id"={eventId} FOR UPDATE
            """).AsNoTracking().ToListAsync(ct);
        if (events.Count == 0) throw new NotFoundException("Alert occurrence not found.");
        var ack = await db.Set<AlertAcknowledgement>().SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.EventId == eventId && x.AuthorId == userId, ct);
        if (ack is null)
        {
            ack = new AlertAcknowledgement { WorkspaceId = workspaceId, EventId = eventId, AuthorId = userId, CreatedAtUtc = await AlertTransactions.NowAsync(db, ct) };
            db.Add(ack); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return ack;
    }
    public async Task<IReadOnlyList<AlertAttemptDiagnostic>> DiagnosticsAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct)
    {
        await AuthorizeAsync(userId, workspaceId, true, ct);
        return await db.Set<AlertEvaluationAttempt>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId && (x.Status == "Failed" || x.Status == "DeadLetter"))
            .OrderBy(x => x.QueueOrder).ThenBy(x => x.Id).Skip(Offset(page)).Take(100)
            .Select(x => new AlertAttemptDiagnostic(x.Id, x.WorkItemId, x.RuleId, x.DeviceIdentifier, x.Status, x.AttemptCount, x.NextAttemptAtUtc, x.Reason)).ToListAsync(ct);
    }
}
