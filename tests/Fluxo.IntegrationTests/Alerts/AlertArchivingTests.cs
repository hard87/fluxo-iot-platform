using Fluxo.Application.Alerts;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Domain.Alerts;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Alerts;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Fluxo.IntegrationTests.Alerts;

public sealed class AlertArchivingTests
{
    private static Task Run(Func<AlertBackendTests.Scenario, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        return DisposableTestDatabase.WithDatabaseAsync("it", async cs => await test(await AlertBackendTests.Scenario.Create(cs)));
    }

    [SkippableFact]
    public Task ArchiveClosesEventsFencesClaimsAndPreservesHistory() => Run(async s =>
    {
        var r = await s.Rule();
        await s.Ingest(1, r.ActivatedAtUtc.AddSeconds(1), 90); await s.Drain();
        await s.Ingest(2, r.ActivatedAtUtc.AddSeconds(2), 91);
        await s.Ingest(3, r.ActivatedAtUtc.AddSeconds(3), 92);
        await using var worker = s.Db();
        var engine = s.Engine(worker);
        var claim = Assert.IsType<AlertClaim>(await engine.ClaimAsync("before-archive"));
        AlertRuleRevision archived;
        await using (var db = s.Db())
        {
            archived = await s.Management(db).ArchiveAsync(s.User.Id, s.Workspace.Id, r.RuleId, new(r.Version), default);
            Assert.False(archived.Enabled);
            Assert.NotNull(archived.ArchivedAtUtc);
            Assert.Equal(s.User.Id, archived.AuthorId);
            Assert.Equal(r.Version + 1, archived.Version);
        }
        await Assert.ThrowsAsync<LostAlertLeaseException>(() => engine.EvaluateAsync(claim));
        await Assert.ThrowsAsync<LostAlertLeaseException>(() => engine.FailAsync(claim));
        await s.Ingest(4, archived.CreatedAtUtc.AddSeconds(1), 95); await s.Drain();
        await using var read = s.Db();
        var manager = s.Management(read);
        Assert.Empty(await manager.RulesAsync(s.User.Id, s.Workspace.Id, 1, default));
        Assert.Single(await manager.RulesAsync(s.User.Id, s.Workspace.Id, 1, default, "archived"));
        Assert.Single(await manager.RulesAsync(s.User.Id, s.Workspace.Id, 1, default, "all"));
        Assert.Equal(3, (await manager.RevisionsAsync(s.User.Id, s.Workspace.Id, r.RuleId, 1, default)).Count);
        var occurrence = await read.Set<AlertEvent>().SingleAsync();
        var history = await manager.HistoryAsync(s.User.Id, s.Workspace.Id, occurrence.Id, default);
        Assert.Equal("Closed", history.Event.Status);
        Assert.Equal(new[] { "Firing", "Closed" }, history.Transitions.Select(x => x.Kind));
        Assert.Equal("RuleArchived", history.Transitions.Last().Reason);
        Assert.Equal(r.Id, history.Revision.Id);
        Assert.Equal(2, history.DeliveryIntents.Count);
        Assert.Empty(await read.Set<AlertRuleState>().ToListAsync());
        Assert.Equal(2, await read.Set<AlertEvaluationAttempt>().CountAsync(x => x.Status == "Skipped" && x.Reason == "RuleArchived"));
        Assert.Equal(3, await read.Set<AlertEvaluationAttempt>().CountAsync());
        Assert.Equal(4, await read.Set<AlertEvaluationWorkItem>().CountAsync(x => x.Status == "Completed"));
        Assert.Equal(4, await read.TelemetryPoints.CountAsync());
        var retry = await manager.ArchiveAsync(s.User.Id, s.Workspace.Id, r.RuleId, new(r.Version), default);
        Assert.Equal(archived.Id, retry.Id);
        Assert.Equal(3, await read.Set<AlertRuleRevision>().CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => manager.SaveAsync(s.User.Id, s.Workspace.Id, r.RuleId,
            s.Request() with { Enabled = true, ExpectedVersion = archived.Version }, default));
    });

    [SkippableFact]
    public Task ArchiveRequiresAdminWorkspaceAccessAndCurrentVersion() => Run(async s =>
    {
        var r = await s.Rule();
        await using var db = s.Db();
        var viewer = new PlatformUser("viewer@example.test", "hash", "salt");
        var stranger = new PlatformUser("stranger@example.test", "hash", "salt");
        var other = new Workspace("other", "Other");
        db.AddRange(viewer, stranger, other,
            new WorkspaceMembership(s.Workspace.Id, viewer.Id, WorkspaceMembershipRole.Viewer),
            new WorkspaceMembership(other.Id, stranger.Id, WorkspaceMembershipRole.Owner));
        await db.SaveChangesAsync();
        var manager = s.Management(db);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.ArchiveAsync(viewer.Id, s.Workspace.Id, r.RuleId, new(r.Version), default));
        await Assert.ThrowsAsync<NotFoundException>(() => manager.ArchiveAsync(stranger.Id, s.Workspace.Id, r.RuleId, new(r.Version), default));
        await Assert.ThrowsAsync<NotFoundException>(() => manager.ArchiveAsync(stranger.Id, other.Id, r.RuleId, new(r.Version), default));
        await Assert.ThrowsAsync<ConflictException>(() => manager.ArchiveAsync(s.User.Id, s.Workspace.Id, r.RuleId, new(r.Version - 1), default));
        Assert.Equal(2, await db.Set<AlertRuleRevision>().CountAsync());
        Assert.Single(await manager.RulesAsync(s.User.Id, s.Workspace.Id, 1, default));
        await Assert.ThrowsAsync<ValidationException>(() => manager.RulesAsync(s.User.Id, s.Workspace.Id, 1, default, "invalid"));
        // Archiving must remain possible after a metric/device becomes ineligible for editing.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE devices SET \"IsActive\"=false WHERE \"Id\"={s.Device.Id}");
        await manager.ArchiveAsync(s.User.Id, s.Workspace.Id, r.RuleId, new(r.Version), default);
        Assert.Single(await manager.RulesAsync(viewer.Id, s.Workspace.Id, 1, default, "archived"));
    });

    [SkippableFact]
    public Task ConcurrentArchivesProduceOneRevisionAndMigrationCanRollback() => Run(async s =>
    {
        var r = await s.Rule();
        async Task<AlertRuleRevision> Archive()
        {
            await using var db = s.Db();
            return await s.Management(db).ArchiveAsync(s.User.Id, s.Workspace.Id, r.RuleId, new(r.Version), default);
        }
        var results = await Task.WhenAll(Archive(), Archive());
        Assert.Equal(results[0].Id, results[1].Id);
        await using var read = s.Db();
        Assert.Equal(3, await read.Set<AlertRuleRevision>().CountAsync());
        await Assert.ThrowsAsync<PostgresException>(() => read.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE alert_rule_revisions SET \"ArchivedAtUtc\"=NULL WHERE \"Id\"={results[0].Id}"));
        var migrations = (await read.Database.GetAppliedMigrationsAsync()).ToArray();
        var migrator = read.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[^2]);
        Assert.Equal(1, await read.Devices.CountAsync());
        await migrator.MigrateAsync();
        Assert.Equal(3, await read.Set<AlertRuleRevision>().CountAsync());
    });
}
