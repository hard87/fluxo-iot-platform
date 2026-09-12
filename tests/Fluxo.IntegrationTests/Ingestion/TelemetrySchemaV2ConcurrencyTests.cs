using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Exceptions;
using Fluxo.Domain.Models;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Options;
using Fluxo.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fluxo.IntegrationTests.Ingestion;

public sealed class TelemetrySchemaV2ConcurrencyTests
{
    [SkippableFact]
    public async Task SameDevice_ConcurrentDiscoveries_AcceptsExactlyTwenty()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid(); const string device = "guard-device";
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(1, 21).Select(async i =>
            {
                await start.Task;
                await using var db = CreateContext(cs);
                var repo = new TelemetryIngestionRepository(db);
                var record = Record(workspace, device, i);
                try { return (Fluxo.Application.Models.TelemetryIngestionWriteResult?)await repo.AddWithPointsAsync(record, [new($"metric_{i:00}", MetricValueType.Numeric, NumericValue: i)]); }
                catch (MetricCardinalityGuardException) { return (Fluxo.Application.Models.TelemetryIngestionWriteResult?)null; }
            }).ToArray();
            start.SetResult(); await Task.WhenAll(tasks);
            await using var verify = CreateContext(cs);
            Assert.Equal(20, tasks.Count(x => x.Result is not null));
            Assert.Equal(20, await verify.MetricDefinitions.CountAsync(x => x.WorkspaceId == workspace));
            Assert.Equal(20, await verify.MetricDefinitionDiscoveryAudits.CountAsync(x => x.WorkspaceId == workspace && x.DeviceId == device));
            Assert.Equal(20, await verify.TelemetryIngestionRecords.CountAsync(x => x.WorkspaceId == workspace));
        });
    }

    [SkippableFact]
    public async Task DifferentDevices_SameNewKey_ConvergeWithoutDuplicateClassification()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Run(string device)
            {
                await start.Task; await using var db = CreateContext(cs);
                var result = await new TelemetryIngestionRepository(db).AddWithPointsAsync(
                    Record(workspace, device, 1), [new("motor_current", MetricValueType.Numeric, NumericValue: 8.1)]);
                Assert.Equal(Fluxo.Application.Models.TelemetryIngestionWriteResult.Persisted, result);
            }
            var a = Run("device-a"); var b = Run("device-b"); start.SetResult(); await Task.WhenAll(a, b);
            await using var verify = CreateContext(cs);
            var definition = await verify.MetricDefinitions.SingleAsync(x => x.WorkspaceId == workspace && x.MetricKey == "motor_current");
            Assert.Equal(2, await verify.TelemetryIngestionRecords.CountAsync(x => x.WorkspaceId == workspace));
            Assert.Equal(2, await verify.TelemetryPoints.CountAsync(x => x.WorkspaceId == workspace && x.MetricDefinitionId == definition.Id));
        });
    }

    [SkippableFact]
    public async Task WriterFailure_RollsBackEntireIngestionUnit()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid(); var record = Record(workspace, "fault-device", 1);
            await using (var db = CreateContext(cs))
            {
                var repo = new TelemetryIngestionRepository(db, new ThrowingWriter());
                await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddWithPointsAsync(
                    record, [new("pressure", MetricValueType.Numeric, NumericValue: 2.4)]));
            }
            await using var verify = CreateContext(cs);
            Assert.False(await verify.TelemetryIngestionRecords.AnyAsync(x => x.Id == record.Id));
            Assert.False(await verify.TelemetryPoints.AnyAsync(x => x.IngestionRecordId == record.Id));
            Assert.False(await verify.MetricDefinitions.AnyAsync(x => x.WorkspaceId == workspace));
            Assert.False(await verify.MetricDefinitionDiscoveryAudits.AnyAsync(x => x.IngestionRecordId == record.Id));
        });
    }

    [SkippableFact]
    public async Task BinaryCopyFailure_RollsBackEntireIngestionUnit()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid();
            var record = new TelemetryIngestionRecord("bench", workspace, "copy-fault", "telemetry", "topic", "2",
                new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow, "{\"sequence\":1}", sequence: 1);
            await using (var db = CreateContext(cs))
            {
                var repo = new TelemetryIngestionRepository(db, new NpgsqlBinaryCopyTelemetryPointWriter(db));
                await Assert.ThrowsAnyAsync<PostgresException>(() => repo.AddWithPointsAsync(record,
                    [new("future_metric", MetricValueType.Numeric, NumericValue: 1)]));
            }
            await using var verify = CreateContext(cs);
            Assert.False(await verify.TelemetryIngestionRecords.AnyAsync(x => x.Id == record.Id));
            Assert.False(await verify.MetricDefinitions.AnyAsync(x => x.WorkspaceId == workspace));
            Assert.False(await verify.MetricDefinitionDiscoveryAudits.AnyAsync(x => x.IngestionRecordId == record.Id));
            Assert.False(await verify.TelemetryPoints.AnyAsync(x => x.IngestionRecordId == record.Id));
        });
    }

    [SkippableFact]
    public async Task WorkspaceLimit_AllowsFiveHundredthAndExistingButRejectsNewAtomically()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid();
            await using (var seed = CreateContext(cs))
            {
                seed.MetricDefinitions.AddRange(Enumerable.Range(1, 499).Select(i =>
                    new MetricDefinition(workspace, "bench", $"seed_{i:000}", MetricValueType.Numeric, DateTime.UtcNow)));
                await seed.SaveChangesAsync();
            }
            await using (var db = CreateContext(cs))
            {
                var repo = new TelemetryIngestionRepository(db);
                Assert.Equal(Fluxo.Application.Models.TelemetryIngestionWriteResult.Persisted,
                    await repo.AddWithPointsAsync(Record(workspace, "limit-device", 1), [new("metric_500", MetricValueType.Numeric, NumericValue: 1)]));
                Assert.Equal(Fluxo.Application.Models.TelemetryIngestionWriteResult.Persisted,
                    await repo.AddWithPointsAsync(Record(workspace, "limit-device", 2), [new("metric_500", MetricValueType.Numeric, NumericValue: 2)]));
                await Assert.ThrowsAsync<MetricWorkspaceLimitException>(() => repo.AddWithPointsAsync(
                    Record(workspace, "limit-device", 3), [new("metric_500", MetricValueType.Numeric, NumericValue: 3), new("metric_501", MetricValueType.Numeric, NumericValue: 4)]));
            }
            await using var verify = CreateContext(cs);
            Assert.Equal(500, await verify.MetricDefinitions.CountAsync(x => x.WorkspaceId == workspace));
            Assert.False(await verify.MetricDefinitions.AnyAsync(x => x.WorkspaceId == workspace && x.MetricKey == "metric_501"));
            Assert.False(await verify.TelemetryIngestionRecords.AnyAsync(x => x.WorkspaceId == workspace && x.Sequence == 3));
        });
    }

    [SkippableFact]
    public async Task DiscoveryWindow_UsesDatabaseCurrentTimestamp()
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid(); const string device = "clock-device";
            await using (var seed = CreateContext(cs))
            {
                for (var i = 1; i <= 20; i++)
                {
                    var record = Record(workspace, device, i); seed.TelemetryIngestionRecords.Add(record);
                    seed.MetricDefinitionDiscoveryAudits.Add(new(workspace, "bench", device, $"old_{i:00}", record.Id));
                }
                await seed.SaveChangesAsync();
                await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE metric_definition_discovery_audits SET \"DiscoveredAtUtc\" = CURRENT_TIMESTAMP - INTERVAL '61 minutes' WHERE \"WorkspaceId\" = {workspace}");
            }
            await using (var db = CreateContext(cs))
            {
                var repo = new TelemetryIngestionRepository(db);
                Assert.Equal(Fluxo.Application.Models.TelemetryIngestionWriteResult.Persisted,
                    await repo.AddWithPointsAsync(Record(workspace, device, 21), [new("inside_window_probe", MetricValueType.Numeric, NumericValue: 1)]));
            }
            await using var verify = CreateContext(cs);
            Assert.Equal(21, await verify.MetricDefinitionDiscoveryAudits.CountAsync(x => x.WorkspaceId == workspace));
        });
    }

    [SkippableTheory]
    [InlineData(MetricValueType.Numeric, MetricValueType.Boolean)]
    [InlineData(MetricValueType.Numeric, MetricValueType.Text)]
    [InlineData(MetricValueType.Boolean, MetricValueType.Numeric)]
    [InlineData(MetricValueType.Boolean, MetricValueType.Text)]
    [InlineData(MetricValueType.Text, MetricValueType.Numeric)]
    [InlineData(MetricValueType.Text, MetricValueType.Boolean)]
    public async Task MetricTypeMismatch_AllRelevantPermutations_AreRejectedAtomically(
        MetricValueType establishedType,
        MetricValueType incomingType)
    {
        await WithDatabaseAsync(async cs =>
        {
            var workspace = Guid.NewGuid();
            await using (var seed = CreateContext(cs))
            {
                seed.MetricDefinitions.Add(new MetricDefinition(workspace, "bench", "stable_metric", establishedType, DateTime.UtcNow));
                await seed.SaveChangesAsync();
            }

            var record = Record(workspace, $"mismatch-{establishedType}-{incomingType}".ToLowerInvariant(), 1);
            var metric = Metric("stable_metric", incomingType);
            await using (var db = CreateContext(cs))
            {
                var repo = new TelemetryIngestionRepository(db);
                await Assert.ThrowsAsync<MetricTypeMismatchException>(() => repo.AddWithPointsAsync(record, [metric]));
            }

            await using var verify = CreateContext(cs);
            Assert.False(await verify.TelemetryIngestionRecords.AnyAsync(x => x.Id == record.Id));
            Assert.False(await verify.TelemetryPoints.AnyAsync(x => x.IngestionRecordId == record.Id));
            Assert.Single(await verify.MetricDefinitions.Where(x => x.WorkspaceId == workspace).ToListAsync());
            Assert.False(await verify.MetricDefinitionDiscoveryAudits.AnyAsync(x => x.IngestionRecordId == record.Id));
        });
    }

    [SkippableFact]
    public async Task PartitionMaintenance_IsConcurrentIdempotent_AndHealthReflectsDatabase()
    {
        await WithDatabaseAsync(async cs =>
        {
            var services = new ServiceCollection().AddDbContext<FluxoDbContext>(o => o.UseNpgsql(cs))
                .AddSingleton<TelemetryPartitionState>().BuildServiceProvider();
            var state = services.GetRequiredService<TelemetryPartitionState>();
            var first = new TelemetryPartitionMaintenanceService(services.GetRequiredService<IServiceScopeFactory>(), state, NullLogger<TelemetryPartitionMaintenanceService>.Instance);
            var second = new TelemetryPartitionMaintenanceService(services.GetRequiredService<IServiceScopeFactory>(), state, NullLogger<TelemetryPartitionMaintenanceService>.Instance);
            await Task.WhenAll(first.MaintainAsync(), second.MaintainAsync());
            await first.MaintainAsync();
            await using (var db = CreateContext(cs))
            {
                var count = await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_inherits i JOIN pg_class p ON p.oid=i.inhparent WHERE p.relname='telemetry_points'").SingleAsync();
                Assert.Equal(8, count);
            }
            var health = new TelemetryPartitionHealthCheck(state);
            Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new())).Status);

            var nextName = $"telemetry_points_{DateTime.UtcNow.AddMonths(1):yyyy_MM}";
            await using (var db = CreateContext(cs))
            {
                var sql = $"DROP TABLE {nextName}";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            await first.RefreshStateAsync();
            Assert.Equal(HealthStatus.Unhealthy, (await health.CheckHealthAsync(new())).Status);

            await first.MaintainAsync();
            await using (var db = CreateContext(cs))
                for (var i = 3; i <= 6; i++)
                {
                    var sql = $"DROP TABLE telemetry_points_{DateTime.UtcNow.AddMonths(i):yyyy_MM}";
                    await db.Database.ExecuteSqlRawAsync(sql);
                }
            await first.RefreshStateAsync();
            Assert.Equal(HealthStatus.Degraded, (await health.CheckHealthAsync(new())).Status);
            await services.DisposeAsync();
        });
    }

    [SkippableFact]
    public async Task PartitionMaintenance_ControlledFailure_TransitionsHealthToUnhealthy()
    {
        await WithDatabaseAsync(async cs =>
        {
            var services = new ServiceCollection().AddDbContext<FluxoDbContext>(o => o.UseNpgsql(cs))
                .AddSingleton<TelemetryPartitionState>().BuildServiceProvider();
            var state = services.GetRequiredService<TelemetryPartitionState>();
            var maintenance = new TelemetryPartitionMaintenanceService(
                services.GetRequiredService<IServiceScopeFactory>(),
                state,
                NullLogger<TelemetryPartitionMaintenanceService>.Instance);
            var health = new TelemetryPartitionHealthCheck(state);

            await maintenance.MaintainAsync();
            Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new())).Status);

            await using (var db = CreateContext(cs))
            {
                const string sql = "DROP TABLE telemetry_points CASCADE";
                await db.Database.ExecuteSqlRawAsync(sql);
            }

            await Assert.ThrowsAsync<PostgresException>(() => maintenance.MaintainAsync());
            Assert.Equal(HealthStatus.Unhealthy, (await health.CheckHealthAsync(new())).Status);
            Assert.NotNull(state.LastFailureUtc);

            await services.DisposeAsync();
        });
    }

    private sealed class ThrowingWriter : ITelemetryPointWriter
    { public Task WriteAsync(IReadOnlyCollection<TelemetryPoint> points, CancellationToken cancellationToken = default) => throw new InvalidOperationException("fault injection"); }

    private static TelemetryMetricValue Metric(string key, MetricValueType type) => type switch
    {
        MetricValueType.Numeric => new(key, MetricValueType.Numeric, NumericValue: 1.23),
        MetricValueType.Boolean => new(key, MetricValueType.Boolean, BooleanValue: true),
        MetricValueType.Text => new(key, MetricValueType.Text, TextValue: "running"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static TelemetryIngestionRecord Record(Guid workspace, string device, long sequence) => new(
        "bench", workspace, device, "telemetry", $"fluxo/tenants/bench/workspaces/{workspace}/devices/{device}/telemetry",
        "2", DateTime.UtcNow, DateTime.UtcNow, $"{{\"sequence\":{sequence}}}", sequence: sequence);

    private static FluxoDbContext CreateContext(string cs) => new(new DbContextOptionsBuilder<FluxoDbContext>().UseNpgsql(cs).Options);

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        await DisposableTestDatabase.WithDatabaseAsync("v2", async connectionString =>
        {
            await using (var db = CreateContext(connectionString))
            {
                await db.Database.MigrateAsync();
            }

            await test(connectionString);
        });
    }
}
