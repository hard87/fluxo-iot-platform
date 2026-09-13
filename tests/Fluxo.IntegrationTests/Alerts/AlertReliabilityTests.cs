using Fluxo.Application.Alerts;
using Fluxo.Domain.Alerts;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.IntegrationTests.Alerts;

/// <summary>
/// E2.3 of docs/plano-acao-e1-e2-e3-e5.md: closes the specific reliability/isolation gaps that
/// AlertBackendTests.cs does not cover -- duration/gap/hysteresis/cooldown, duplicate/rejected
/// ingestion, reclaim/lease expiry, crash rollback, retry/backoff/dead-letter, cross-workspace
/// isolation, edit fencing and late-arriving telemetry are already proven there and are not
/// repeated here. This file adds: a global rule's per-device state isolation (no test in the
/// repo used more than one device), and two genuinely concurrent real AlertEvaluationWorker
/// instances racing on the same database (the existing concurrency test drives two manually
/// constructed engines through a single synchronized claim, never two live worker loops).
/// </summary>
public sealed class AlertReliabilityTests
{
    [SkippableFact]
    public Task GlobalRule_KeepsIndependentStatePerDevice() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        var deviceB = await harness.AddDeviceAsync("sensor-2");

        var rule = await harness.CreateRuleAsync(new SaveAlertRuleRequest(
            "Global high temperature", harness.Metric.Id, null, "GreaterThan", 80, Hysteresis: 2));
        var at = rule.ActivatedAtUtc.AddSeconds(1);

        // Device A (the harness's default device) fires and resolves.
        await harness.PublishTelemetryAsync(1, at, 90);
        await harness.WaitForAsync(async db => await db.Set<AlertEvent>()
            .CountAsync(x => x.Status == "Firing" && x.DeviceIdentifier == harness.Device.Identifier) == 1);
        await harness.PublishTelemetryAsync(2, at.AddSeconds(1), 70);
        await harness.WaitForAsync(async db =>
            (await db.Set<AlertEvent>().SingleAsync(x => x.DeviceIdentifier == harness.Device.Identifier)).Status == "Resolved");

        // Device B never received telemetry -- the global rule firing/resolving for A must never
        // leak an event, transition or state row into B.
        await using (var db = harness.Db())
            Assert.Equal(0, await db.Set<AlertEvent>().CountAsync(x => x.DeviceIdentifier == deviceB.Identifier));

        // Device B now fires independently, unaffected by A's prior history under the same rule.
        await harness.PublishTelemetryAsync(1, at.AddSeconds(2), 95, deviceIdentifier: deviceB.Identifier);
        await harness.WaitForAsync(async db => await db.Set<AlertEvent>()
            .CountAsync(x => x.Status == "Firing" && x.DeviceIdentifier == deviceB.Identifier) == 1);

        await using var final = harness.Db();
        Assert.Equal(2, await final.Set<AlertEvent>().CountAsync());
        Assert.Equal("Resolved", (await final.Set<AlertEvent>().SingleAsync(x => x.DeviceIdentifier == harness.Device.Identifier)).Status);
        Assert.Equal("Firing", (await final.Set<AlertEvent>().SingleAsync(x => x.DeviceIdentifier == deviceB.Identifier)).Status);
    });

    [SkippableFact]
    public Task ConcurrentRealWorkers_ProduceNoDuplicateEvents() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs, pollIntervalMilliseconds: 20);
        await using var second = await AlertWorkerHarness.StartAdditionalWorkerAsync(cs, pollIntervalMilliseconds: 20);

        var deviceB = await harness.AddDeviceAsync("sensor-2");
        var ruleA = await harness.CreateRuleAsync(harness.Request());
        var ruleB = await harness.CreateRuleAsync(new SaveAlertRuleRequest(
            "High B", harness.Metric.Id, deviceB.Identifier, "GreaterThan", 80, Hysteresis: 2));

        await Task.WhenAll(
            harness.PublishTelemetryAsync(1, ruleA.ActivatedAtUtc.AddSeconds(1), 90),
            harness.PublishTelemetryAsync(1, ruleB.ActivatedAtUtc.AddSeconds(1), 95, deviceIdentifier: deviceB.Identifier));

        await harness.WaitForAsync(async db => await db.Set<AlertEvent>().CountAsync(x => x.Status == "Firing") == 2);

        // Give both live workers a further beat: if SKIP LOCKED/claim fencing were broken, a
        // second worker racing on the same work item would show up here as a duplicate.
        await Task.Delay(300);

        await using var read = harness.Db();
        var events = await read.Set<AlertEvent>().ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Single(events, x => x.DeviceIdentifier == harness.Device.Identifier && x.Status == "Firing");
        Assert.Single(events, x => x.DeviceIdentifier == deviceB.Identifier && x.Status == "Firing");
    });

    [SkippableFact]
    public Task DeliveryIntent_IsTrackedButNoChannelAdapterExistsYet() => Run(async cs =>
    {
        // E2.4 gate (docs/plano-acao-e1-e2-e3-e5.md:187-192): no WebhookDelivery/NotificationDelivery
        // adapter exists in this codebase today, only the channel-neutral AlertDeliveryIntent
        // tracking record. This test documents exactly what the code produces today, without
        // inventing a provider or channel choice that hasn't been implemented.
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        var rule = await harness.CreateRuleAsync();
        await harness.PublishTelemetryAsync(1, rule.ActivatedAtUtc.AddSeconds(1), 90);
        await harness.WaitForAsync(async db => await db.Set<AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        await using var db = harness.Db();
        Assert.Single(await db.Set<AlertDeliveryIntent>().ToListAsync());
    });

    private static Task Run(Func<string, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        return DisposableTestDatabase.WithDatabaseAsync("it", test);
    }
}
