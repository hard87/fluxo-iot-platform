using Fluxo.Application.Alerts;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Alerts;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Alerts;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.IntegrationTests.Infrastructure;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fluxo.IntegrationTests.Alerts;

public sealed class AlertBackendTests
{
    [SkippableFact]
    public Task DurationGapHysteresisCooldown_AndBooleanRules_WorkOnPostgres() => Run(async s =>
    {
        await using (var configure = s.Db())
            await configure.Database.ExecuteSqlInterpolatedAsync($"UPDATE metric_definitions SET \"ExpectedIntervalSec\"=2 WHERE \"Id\"={s.Metric.Id}");
        var r = await s.Rule(s.Request() with { DurationSeconds = 5, CooldownSeconds = 20 });
        var at = r.ActivatedAtUtc;
        await s.Ingest(1, at.AddSeconds(1), 90); await s.Ingest(2, at.AddSeconds(3), 90);
        await s.Ingest(3, at.AddSeconds(10), 90); // gap resets duration
        await s.Ingest(4, at.AddSeconds(14), 90); await s.Drain();
        await using (var db = s.Db()) Assert.Equal(0, await db.Set<AlertEvent>().CountAsync());
        await s.Ingest(5, at.AddSeconds(15), 90); await s.Ingest(6, at.AddSeconds(16), 79); await s.Drain();
        await using (var db = s.Db()) Assert.Equal("Firing", (await db.Set<AlertEvent>().SingleAsync()).Status);
        await s.Ingest(7, at.AddSeconds(17), 78); await s.Ingest(8, at.AddSeconds(18), 90);
        await s.Ingest(9, at.AddSeconds(23), 90); await s.Ingest(10, at.AddSeconds(28), 90);
        await s.Ingest(11, at.AddSeconds(33), 90); await s.Drain();
        await using (var db = s.Db()) Assert.Equal("Resolved", (await db.Set<AlertEvent>().SingleAsync()).Status);
        await s.Ingest(12, at.AddSeconds(35), 90); await s.Drain();
        await using (var db = s.Db()) Assert.Equal(2, await db.Set<AlertEvent>().CountAsync());
        var boolean = new MetricDefinition(s.Workspace.Id, s.Workspace.TenantId, "door", MetricValueType.Boolean, DateTime.UtcNow);
        await using (var db = s.Db()) { db.Add(boolean); await db.SaveChangesAsync(); }
        var b = await s.Rule(new("Door", boolean.Id, null, "IsTrue"));
        await s.Ingest(13, b.ActivatedAtUtc.AddSeconds(1), true, "door");
        await s.Ingest(14, b.ActivatedAtUtc.AddSeconds(2), false, "door"); await s.Drain();
        await using var read = s.Db();
        Assert.Equal("Resolved", (await read.Set<AlertEvent>().SingleAsync(x => x.RuleId == b.RuleId)).Status);
    });

    [SkippableFact]
    public Task Demo_CreateIngestFireResolveHistory_AndNoWorkForRejectedOrDuplicate() => Run(async s =>
    {
        var revision = await s.Rule();
        var at = revision.ActivatedAtUtc.AddSeconds(1);
        await s.Ingest(1, at, 90);
        await s.Ingest(1, at, 90); // duplicate
        await s.Ingest(2, DateTime.UtcNow.AddDays(-40), 90); // rejected before repository
        await s.Drain();
        await using (var db = s.Db())
        {
            Assert.Equal(1, await db.Set<AlertEvaluationWorkItem>().CountAsync());
            Assert.Equal(1, await db.Set<AlertEvent>().CountAsync(x => x.Status == "Firing"));
            Assert.Equal(2, await db.TelemetryIngestionRejectionRecords.CountAsync());
        }
        await s.Ingest(3, at.AddSeconds(1), 70);
        await s.Drain();
        await using var read = s.Db();
        var occurrence = await read.Set<AlertEvent>().SingleAsync();
        var management = s.Management(read);
        var ack1 = await management.AcknowledgeAsync(s.User.Id, s.Workspace.Id, occurrence.Id, default);
        var ack2 = await management.AcknowledgeAsync(s.User.Id, s.Workspace.Id, occurrence.Id, default);
        Assert.Equal(ack1.Id, ack2.Id);
        var history = await management.HistoryAsync(s.User.Id, s.Workspace.Id, occurrence.Id, default);
        Assert.Equal("Resolved", history.Event.Status);
        Assert.Equal(new[] { "Firing", "Resolved" }, history.Transitions.Select(x => x.Kind));
        Assert.Equal(2, history.DeliveryIntents.Count);
        Assert.Single(history.Acknowledgements);
        Assert.All(history.Transitions, x => Assert.Equal("alerts-v1", x.EvaluatorVersion));
    });

    [SkippableFact]
    public Task TwoItemsSameState_AreSerializedAndNotDuplicated() => Run(async s =>
    {
        var r = await s.Rule();
        await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90);
        await s.Ingest(2, r.ActivatedAtUtc.AddSeconds(2), 91);
        await using var first = s.Db();
        var barrier = new BarrierProbe();
        var engine = s.Engine(first, barrier);
        var claim = Assert.IsType<AlertClaim>(await engine.ClaimAsync("first"));
        var evaluating = engine.EvaluateAsync(claim);
        await barrier.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await using var other = s.Db();
            Assert.Null(await s.Engine(other).ClaimAsync("second"));
        }
        finally { barrier.Release.TrySetResult(); }
        await evaluating;
        await s.Drain();
        await using var read = s.Db();
        Assert.Single(await read.Set<AlertEvent>().ToListAsync());
        Assert.Single(await read.Set<AlertEventTransition>().ToListAsync());
        Assert.Single(await read.Set<AlertDeliveryIntent>().ToListAsync());
        Assert.Equal(2, await read.Set<AlertEvaluationAttempt>().CountAsync(x => x.Status == "Completed"));
    });

    [SkippableFact]
    public Task ReclaimChangesToken_StaleWorkerCannotCommit() => Run(async s =>
    {
        var r = await s.Rule(); await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90);
        await using var oldDb = s.Db();
        var oldEngine = s.Engine(oldDb);
        var oldClaim = Assert.IsType<AlertClaim>(await oldEngine.ClaimAsync("old"));
        await s.Expire(oldClaim);
        await using var newDb = s.Db();
        var newEngine = s.Engine(newDb);
        var newClaim = Assert.IsType<AlertClaim>(await newEngine.ClaimAsync("new"));
        Assert.NotEqual(oldClaim.Token, newClaim.Token);
        await Assert.ThrowsAsync<LostAlertLeaseException>(() => oldEngine.EvaluateAsync(oldClaim));
        await newEngine.EvaluateAsync(newClaim);
        await Assert.ThrowsAsync<LostAlertLeaseException>(() => oldEngine.FailAsync(oldClaim));
        await using var read = s.Db();
        Assert.Equal(1, await read.Set<AlertDeliveryIntent>().CountAsync());
    });

    [SkippableFact]
    public Task CrashAfterStateSave_RollsBackWholeUnit_AndCanRecover() => Run(async s =>
    {
        var r = await s.Rule(); await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90);
        AlertClaim claim;
        await using (var crashed = s.Db())
        {
            var engine = s.Engine(crashed, new ThrowProbe());
            claim = Assert.IsType<AlertClaim>(await engine.ClaimAsync("crashed"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.EvaluateAsync(claim));
        }
        await using (var read = s.Db())
        {
            Assert.Equal(0, await read.Set<AlertRuleState>().CountAsync());
            Assert.Equal(0, await read.Set<AlertEvent>().CountAsync());
            Assert.Equal(0, await read.Set<AlertDeliveryIntent>().CountAsync());
            Assert.Equal("Claimed", (await read.Set<AlertEvaluationAttempt>().SingleAsync()).Status);
        }
        await s.Expire(claim); await s.Drain();
        await using var done = s.Db();
        Assert.Equal(1, await done.Set<AlertEventTransition>().CountAsync());
    });

    [SkippableFact]
    public Task FailureRespectsBackoff_IndependentRuleContinues_DeadLetterObservable() => Run(async s =>
    {
        var r = await s.Rule(); await s.Rule();
        await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(10), 90);
        await s.Ingest(2, r.ActivatedAtUtc.AddSeconds(11), 91);
        await using var failed = s.Db();
        var engine = s.Engine(failed, maxAttempts: 2);
        var claim = Assert.IsType<AlertClaim>(await engine.ClaimAsync("fails"));
        await engine.FailAsync(claim);
        await s.Drain(); // only independent rule can advance
        await using (var read = s.Db())
        {
            Assert.Equal(1, await read.Set<AlertEvaluationAttempt>().CountAsync(x => x.Status == "Failed"));
            Assert.Equal(1, await read.Set<AlertEvaluationAttempt>().CountAsync(x => x.Status == "Pending"));
            Assert.Equal(2, await read.Set<AlertEvaluationAttempt>().CountAsync(x => x.Status == "Completed"));
            await read.Database.ExecuteSqlInterpolatedAsync($"UPDATE alert_evaluation_attempts SET \"NextAttemptAtUtc\"=clock_timestamp()-interval '1 second' WHERE \"Id\"={claim.AttemptId}");
        }
        var retry = Assert.IsType<AlertClaim>(await engine.ClaimAsync("retry"));
        Assert.Equal(claim.AttemptId, retry.AttemptId);
        await engine.FailAsync(retry);
        await s.Drain();
        await using var final = s.Db();
        var diagnostics = await s.Management(final).DiagnosticsAsync(s.User.Id, s.Workspace.Id, 1, default);
        Assert.Single(diagnostics); Assert.Equal("DeadLetter", diagnostics[0].Status);
        Assert.Contains(await final.Set<AlertEvaluationWorkItem>().ToListAsync(), x => x.Status == "CompletedWithFailures");
    });

    [SkippableFact]
    public Task EditClosesWithoutRecovery_AndFencesPreviouslyClaimedWork() => Run(async s =>
    {
        var r = await s.Rule(); await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90); await s.Drain();
        await s.Ingest(2, r.ActivatedAtUtc.AddSeconds(2), 91);
        await using var worker = s.Db();
        var engine = s.Engine(worker);
        var claim = Assert.IsType<AlertClaim>(await engine.ClaimAsync("before-edit"));
        await using (var editing = s.Db())
            await s.Management(editing).SaveAsync(s.User.Id, s.Workspace.Id, r.RuleId,
                s.Request() with { Enabled = false, ExpectedVersion = r.Version }, default);
        await Assert.ThrowsAsync<LostAlertLeaseException>(() => engine.EvaluateAsync(claim));
        await using var read = s.Db();
        Assert.Equal("Closed", (await read.Set<AlertEvent>().SingleAsync()).Status);
        Assert.DoesNotContain(await read.Set<AlertEventTransition>().ToListAsync(), x => x.Kind == "Resolved");
        Assert.Equal(3, await read.Set<AlertRuleRevision>().CountAsync());
        Assert.Equal(2, await read.Set<AlertDeliveryIntent>().CountAsync());
        Assert.Equal(0, await read.Set<AlertRuleState>().CountAsync());
    });

    [SkippableFact]
    public Task HistoricalAndBeforeActivationSamples_DoNotRetroactState() => Run(async s =>
    {
        var r = await s.Rule();
        await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(-1), 90);
        await s.Ingest(2, r.ActivatedAtUtc.AddSeconds(10), 90);
        await s.Ingest(3, r.ActivatedAtUtc.AddSeconds(5), 50);
        await s.Drain();
        await using var read = s.Db();
        Assert.Equal(3, await read.TelemetryPoints.CountAsync());
        Assert.Equal("Firing", (await read.Set<AlertEvent>().SingleAsync()).Status);
        var reasons = await read.Set<AlertEvaluationAttempt>().Select(x => x.Reason).ToListAsync();
        Assert.Contains("BeforeActivation", reasons); Assert.Contains("Historical", reasons);
    });

    [SkippableFact]
    public Task CrossWorkspaceReadsWritesAndForeignKeys_AreRefused() => Run(async s =>
    {
        var r = await s.Rule(); await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90); await s.Drain();
        await using var db = s.Db();
        var other = new Workspace("other", "Other"); db.Add(other);
        var stranger = new PlatformUser("stranger@example.test", "hash", "salt"); db.Add(stranger);
        db.Add(new WorkspaceMembership(other.Id, stranger.Id, WorkspaceMembershipRole.Owner));
        var metric = new MetricDefinition(other.Id, "other", "foreign", MetricValueType.Numeric, DateTime.UtcNow); db.Add(metric);
        await db.SaveChangesAsync();
        var manager = s.Management(db);
        await Assert.ThrowsAsync<NotFoundException>(() => manager.RulesAsync(stranger.Id, s.Workspace.Id, 1, default));
        await Assert.ThrowsAsync<NotFoundException>(() => manager.SaveAsync(stranger.Id, s.Workspace.Id, r.RuleId, s.Request(), default));
        await Assert.ThrowsAsync<NotFoundException>(() => manager.SaveAsync(s.User.Id, s.Workspace.Id, null, s.Request() with { MetricDefinitionId = metric.Id }, default));
        var occurrence = await db.Set<AlertEvent>().SingleAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => manager.HistoryAsync(stranger.Id, other.Id, occurrence.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => manager.AcknowledgeAsync(stranger.Id, other.Id, occurrence.Id, default));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO alert_delivery_intents ("Id","WorkspaceId","TransitionId","CreatedAtUtc")
            VALUES ({Guid.NewGuid()}, {other.Id}, {(db.Set<AlertEventTransition>().Single().Id)}, {DateTime.UtcNow})
            """));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE alert_rule_revisions SET \"Name\"='tampered' WHERE \"Id\"={r.Id}"));
    });

    [SkippableFact]
    public Task Migration_UpDownUp_LeavesTelemetryIntact() => Run(async s =>
    {
        await using var db = s.Db();
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[^2]);
        Assert.Equal(1, await db.Devices.CountAsync());
        await migrator.MigrateAsync();
        Assert.Empty(await db.Set<AlertRule>().ToListAsync());
    });

    private static Task Run(Func<Scenario, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        return DisposableTestDatabase.WithDatabaseAsync("it", async cs => { var s = await Scenario.Create(cs); await test(s); });
    }

    internal sealed class Scenario(string cs, Workspace workspace, PlatformUser user, Device device, MetricDefinition metric)
    {
        public string ConnectionString => cs;
        public Workspace Workspace => workspace;
        public PlatformUser User => user;
        public Device Device => device;
        public MetricDefinition Metric => metric;
        public FluxoDbContext Db() => new(new DbContextOptionsBuilder<FluxoDbContext>().UseNpgsql(cs).Options);
        public AlertManagement Management(FluxoDbContext db) => new(db,
            new GetAuthorizedWorkspaceUseCase(new WorkspaceRepository(db), new WorkspaceMembershipRepository(db)), Options.Create(new AlertEvaluationOptions()));
        public AlertEvaluationEngine Engine(FluxoDbContext db, IAlertEvaluationProbe? probe = null, int maxAttempts = 5) =>
            new(db, Options.Create(new AlertEvaluationOptions { MaxAttempts = maxAttempts, RetryBaseSeconds = 60 }), probe);
        public SaveAlertRuleRequest Request() => new("High temperature", metric.Id, device.Identifier, "GreaterThan", 80, Hysteresis: 2);
        public async Task<AlertRuleRevision> Rule(SaveAlertRuleRequest? request = null)
        {
            await using var db = Db(); var manager = Management(db); var input = request ?? Request();
            var draft = await manager.SaveAsync(user.Id, workspace.Id, null, input with { Enabled = false }, default);
            return await manager.SaveAsync(user.Id, workspace.Id, draft.RuleId, input with { Enabled = true, ExpectedVersion = draft.Version }, default);
        }
        public async Task Ingest(long sequence, DateTime occurred, object value, string metricKey = "temperature")
        {
            await using var db = Db();
            var processor = new TelemetryIngestionProcessor(new TelemetryIngestionRepository(db, new NpgsqlBinaryCopyTelemetryPointWriter(db)),
                new TelemetryIngestionRejectionRepository(db), new DeviceRepository(db), Options.Create(new MqttIngestionOptions { DatabaseRetryCount = 1 }),
                NullLogger<TelemetryIngestionProcessor>.Instance);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 2, sequence, occurredAtUtc = occurred,
                metrics = new Dictionary<string, object> { [metricKey] = value } });
            await processor.ProcessAsync($"fluxo/tenants/{workspace.TenantId}/workspaces/{workspace.Id}/devices/{device.Identifier}/telemetry", payload, DateTime.UtcNow);
        }
        public async Task Drain()
        {
            for (var i = 0; i < 100; i++)
            {
                await using var db = Db(); var engine = Engine(db); var claim = await engine.ClaimAsync("test-drain");
                if (claim is null) return;
                await engine.EvaluateAsync(claim);
            }
            throw new InvalidOperationException("Queue did not drain.");
        }
        public async Task Expire(AlertClaim claim)
        {
            await using var db = Db();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE alert_evaluation_attempts SET \"LeaseUntilUtc\"=clock_timestamp()-interval '1 second' WHERE \"Id\"={claim.AttemptId}");
        }
        public static async Task<Scenario> Create(string cs)
        {
            var workspace = new Workspace("alerts", "Alerts test");
            var user = new PlatformUser("owner@example.test", "hash", "salt");
            var device = new Device(workspace.Id, "Sensor", "sensor-1", DeviceCategory.Sensor, tenantId: workspace.TenantId);
            var metric = new MetricDefinition(workspace.Id, workspace.TenantId, "temperature", MetricValueType.Numeric, DateTime.UtcNow);
            var s = new Scenario(cs, workspace, user, device, metric);
            await using var db = s.Db(); await db.Database.MigrateAsync();
            db.AddRange(workspace, user, device, metric, new WorkspaceMembership(workspace.Id, user.Id, WorkspaceMembershipRole.Owner));
            await db.SaveChangesAsync(); return s;
        }
    }
    private sealed class ThrowProbe : IAlertEvaluationProbe
    {
        public Task AtAsync(string stage, Guid id, CancellationToken ct) => stage == "AfterSaveBeforeCommit"
            ? throw new InvalidOperationException("simulated abrupt termination") : Task.CompletedTask;
    }
    private sealed class BarrierProbe : IAlertEvaluationProbe
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task AtAsync(string stage, Guid id, CancellationToken ct)
        {
            if (stage != "AfterSaveBeforeCommit") return;
            Arrived.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
    }
}
