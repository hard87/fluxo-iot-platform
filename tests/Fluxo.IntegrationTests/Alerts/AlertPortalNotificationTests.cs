using Fluxo.Application.Alerts;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Domain.Enums;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.IntegrationTests.Alerts;

/// <summary>
/// E2.4 (portal channel) of docs/plano-acao-e1-e2-e3-e5.md: NotificationSubscription/PortalNotification
/// wiring through AlertTransactions.Transition, driven end-to-end via the real AlertWorkerHarness
/// (same harness E2 introduced, not a manual drain loop).
/// </summary>
public sealed class AlertPortalNotificationTests
{
    [SkippableFact]
    public Task TwoRecipients_ReceiveOwnNotifications_OnFiringAndResolved() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        var memberB = await harness.AddMemberAsync();

        var rule = await harness.CreateRuleAsync(harness.Request() with
        {
            PortalRecipientUserIds = [harness.User.Id, memberB.Id]
        });
        var at = rule.ActivatedAtUtc.AddSeconds(1);

        await harness.PublishTelemetryAsync(1, at, 90);
        await harness.WaitForAsync(async db => await db.Set<Fluxo.Domain.Alerts.AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        await using (var db = harness.Db())
        {
            var management = harness.Management(db);
            var ownerNotifications = await management.NotificationsAsync(harness.User.Id, harness.Workspace.Id, 1, default);
            var memberNotifications = await management.NotificationsAsync(memberB.Id, harness.Workspace.Id, 1, default);
            Assert.Single(ownerNotifications);
            Assert.Equal("Firing", ownerNotifications[0].TransitionKind);
            Assert.Single(memberNotifications);
            Assert.Equal("Firing", memberNotifications[0].TransitionKind);
        }

        await harness.PublishTelemetryAsync(2, at.AddSeconds(1), 70);
        await harness.WaitForAsync(async db =>
            (await db.Set<Fluxo.Domain.Alerts.AlertEvent>().SingleAsync()).Status == "Resolved");

        await using var final = harness.Db();
        var management2 = harness.Management(final);
        var ownerAfter = await management2.NotificationsAsync(harness.User.Id, harness.Workspace.Id, 1, default);
        var memberAfter = await management2.NotificationsAsync(memberB.Id, harness.Workspace.Id, 1, default);
        Assert.Equal(2, ownerAfter.Count);
        Assert.Contains(ownerAfter, x => x.TransitionKind == "Resolved");
        Assert.Equal(2, memberAfter.Count);
        Assert.Contains(memberAfter, x => x.TransitionKind == "Resolved");
    });

    [SkippableFact]
    public Task EditingRuleToRemoveRecipient_StopsFutureNotifications() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        var memberB = await harness.AddMemberAsync();

        var rule = await harness.CreateRuleAsync(harness.Request() with
        {
            PortalRecipientUserIds = [harness.User.Id, memberB.Id]
        });
        var at = rule.ActivatedAtUtc.AddSeconds(1);
        await harness.PublishTelemetryAsync(1, at, 90);
        await harness.WaitForAsync(async db => await db.Set<Fluxo.Domain.Alerts.AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        // Edit the rule (which resolves the open event as Closed and starts a new revision) to
        // keep only the owner as a portal recipient.
        await using (var edit = harness.Db())
        {
            var management = harness.Management(edit);
            await management.SaveAsync(harness.User.Id, harness.Workspace.Id, rule.RuleId,
                harness.Request() with { Enabled = true, ExpectedVersion = rule.Version, PortalRecipientUserIds = [harness.User.Id] }, default);
        }

        var secondAt = DateTime.UtcNow;
        await harness.PublishTelemetryAsync(2, secondAt.AddSeconds(1), 90);
        await harness.WaitForAsync(async db => await db.Set<Fluxo.Domain.Alerts.AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        await using var final = harness.Db();
        var management2 = harness.Management(final);
        var memberNotifications = await management2.NotificationsAsync(memberB.Id, harness.Workspace.Id, 1, default);
        var ownerNotifications = await management2.NotificationsAsync(harness.User.Id, harness.Workspace.Id, 1, default);
        Assert.Single(memberNotifications); // only the original Firing, nothing from the new revision
        Assert.Equal(2, ownerNotifications.Count); // original Firing + the new revision's Firing
    });

    [SkippableFact]
    public Task SaveWithRecipientNotAMember_IsRejected() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        await using var db = harness.Db();
        var management = harness.Management(db);

        await Assert.ThrowsAsync<ValidationException>(() => management.SaveAsync(harness.User.Id, harness.Workspace.Id, null,
            harness.Request() with { Enabled = false, PortalRecipientUserIds = [Guid.NewGuid()] }, default));

        await using var read = harness.Db();
        Assert.Equal(0, await read.Set<Fluxo.Domain.Alerts.AlertRule>().CountAsync()); // whole transaction rolled back
    });

    [SkippableFact]
    public Task MarkNotificationRead_IsIdempotent_AndOnlyByRecipient() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);
        var memberB = await harness.AddMemberAsync();
        var rule = await harness.CreateRuleAsync(harness.Request() with { PortalRecipientUserIds = [harness.User.Id] });
        await harness.PublishTelemetryAsync(1, rule.ActivatedAtUtc.AddSeconds(1), 90);
        await harness.WaitForAsync(async db => await db.Set<Fluxo.Domain.Alerts.AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        Guid notificationId;
        await using (var db = harness.Db())
        {
            var management = harness.Management(db);
            var notifications = await management.NotificationsAsync(harness.User.Id, harness.Workspace.Id, 1, default);
            notificationId = Assert.Single(notifications).Id;

            // Not the recipient -- must not be able to mark it read.
            await Assert.ThrowsAsync<NotFoundException>(() =>
                management.MarkNotificationReadAsync(memberB.Id, harness.Workspace.Id, notificationId, default));
        }

        await using (var db = harness.Db())
        {
            var management = harness.Management(db);
            await management.MarkNotificationReadAsync(harness.User.Id, harness.Workspace.Id, notificationId, default);
            await management.MarkNotificationReadAsync(harness.User.Id, harness.Workspace.Id, notificationId, default); // idempotent
        }

        await using var final = harness.Db();
        var read = await harness.Management(final).NotificationsAsync(harness.User.Id, harness.Workspace.Id, 1, default);
        Assert.NotNull(Assert.Single(read).ReadAtUtc);
    });

    private static Task Run(Func<string, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        return DisposableTestDatabase.WithDatabaseAsync("it", test);
    }
}
