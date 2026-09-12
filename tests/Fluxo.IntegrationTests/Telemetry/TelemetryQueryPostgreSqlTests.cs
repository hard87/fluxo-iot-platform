using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Application.UseCases.Telemetry;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.IntegrationTests.Telemetry;

public sealed class TelemetryQueryPostgreSqlTests
{
    [SkippableFact]
    public Task Query_UserNotMemberOfWorkspace_Returns404NotFound() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db, member: false);
        await Assert.ThrowsAsync<NotFoundException>(() => UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id,
            Request(seed.Device.Id, seed.Metric.MetricKey), default));
    });

    [SkippableFact]
    public Task Query_DeviceIdFromOtherWorkspace_Returns404NotFound() => WithDatabaseAsync(async db =>
    {
        var a = await SeedAsync(db); var b = await SeedAsync(db);
        await Assert.ThrowsAsync<NotFoundException>(() => UseCase(db).ExecuteAsync(a.User.Id, a.Workspace.Id,
            Request(b.Device.Id, a.Metric.MetricKey), default));
    });

    [SkippableFact]
    public Task Query_MetricKeyFromOtherWorkspace_Returns404NotFound() => WithDatabaseAsync(async db =>
    {
        var a = await SeedAsync(db, metricKey:"metric_a"); var b = await SeedAsync(db, metricKey:"metric_b");
        await Assert.ThrowsAsync<NotFoundException>(() => UseCase(db).ExecuteAsync(a.User.Id, a.Workspace.Id,
            Request(a.Device.Id, b.Metric.MetricKey), default));
    });

    [SkippableFact]
    public Task Query_PartialDeviceOwnership_RejectsEntireRequestNotJustMissingDevice() => WithDatabaseAsync(async db =>
    {
        var a = await SeedAsync(db); var b = await SeedAsync(db);
        var request = Request(a.Device.Id, a.Metric.MetricKey) with { DeviceIds = [a.Device.Id, b.Device.Id] };
        await Assert.ThrowsAsync<NotFoundException>(() => UseCase(db).ExecuteAsync(a.User.Id, a.Workspace.Id, request, default));
    });

    [SkippableFact]
    public Task Query_ValidRequest_NeverReturnsPointsFromOtherWorkspace() => WithDatabaseAsync(async db =>
    {
        var a = await SeedAsync(db, value:11); _ = await SeedAsync(db, value:99);
        var response = await UseCase(db).ExecuteAsync(a.User.Id, a.Workspace.Id, Request(a.Device.Id, a.Metric.MetricKey), default);
        Assert.All(response.Series.SelectMany(x=>x.Points), x => Assert.Equal(11, x.NumericValue));
    });

    [SkippableFact]
    public Task Query_Raw_ReturnsPointsInAscendingOrder() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db, value:3, pointOffsets:[30, 10, 20]);
        var response = await UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id, Request(seed.Device.Id, seed.Metric.MetricKey), default);
        Assert.Equal(response.Series[0].Points.OrderBy(x=>x.TimestampUtc).Select(x=>x.TimestampUtc), response.Series[0].Points.Select(x=>x.TimestampUtc));
    });

    [SkippableFact]
    public Task Query_Aggregated_BucketBoundariesUseUtc() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db, value:2, pointOffsets:[1, 61]);
        var request = Request(seed.Device.Id, seed.Metric.MetricKey) with { Aggregation="avg", Bucket="1m" };
        var response = await UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id, request, default);
        Assert.All(response.Series[0].Points, x => { Assert.Equal(DateTimeKind.Utc, x.TimestampUtc.Kind); Assert.Equal(0, x.TimestampUtc.Second); });
    });

    [SkippableFact]
    public Task Query_EmptySeriesForDeviceWithNoDataInPeriod_ReturnsEmptyPointsNotError() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db, pointOffsets:[]);
        var response = await UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id, Request(seed.Device.Id, seed.Metric.MetricKey), default);
        Assert.Empty(response.Series[0].Points);
    });

    [SkippableFact]
    public Task Query_RawExceedsPerSeriesLimit_SetsTruncatedTrue() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db, pointOffsets:[]); var ingestion = new TelemetryIngestionRecord(seed.Workspace.TenantId,
            seed.Workspace.Id, seed.Device.Identifier, "telemetry", "topic", "2", Base, Base, "{}", sequence:90000);
        db.Add(ingestion); await db.SaveChangesAsync();
        var points = Enumerable.Range(0, 20_001).Select(i => new TelemetryPoint(seed.Workspace.TenantId,
            seed.Workspace.Id, seed.Device.Identifier, seed.Metric.Id, Base.AddMilliseconds(i*100), ingestion.Id, numeric:i)).ToArray();
        await new NpgsqlBinaryCopyTelemetryPointWriter(db).WriteAsync(points);
        var response = await UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id,
            Request(seed.Device.Id, seed.Metric.MetricKey), default);
        Assert.Equal(20_000, response.Series[0].Points.Count); Assert.True(response.Series[0].Truncated);
    });

    [SkippableFact]
    public Task Query_CancelledToken_ThrowsWithoutExecuting() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UseCase(db).ExecuteAsync(seed.User.Id, seed.Workspace.Id,
            Request(seed.Device.Id, seed.Metric.MetricKey), cts.Token));
    });

    [SkippableFact]
    public Task MetricDefinitions_List_OnlyReturnsIsQueryableTrue() => WithDatabaseAsync(async db =>
    {
        var seed = await SeedAsync(db); var hidden = new MetricDefinition(seed.Workspace.Id, seed.Workspace.TenantId, "hidden", MetricValueType.Text, DateTime.UtcNow);
        db.MetricDefinitions.Add(hidden); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE metric_definitions SET \"IsQueryable\"=false WHERE \"Id\"={hidden.Id}");
        var rows = await new ListWorkspaceMetricDefinitionsUseCase(Auth(db), new TelemetryQueryRepository(db))
            .ExecuteAsync(seed.User.Id, seed.Workspace.Id, default);
        Assert.DoesNotContain(rows, x=>x.Id==hidden.Id); Assert.Contains(rows, x=>x.Id==seed.Metric.Id);
    });

    [Fact]
    public void Query_AggregatedEstimateExceedsMaxPoints_Returns400BeforeExecutingQuery()
    {
        var request = Request(Guid.NewGuid(), "metric") with { FromUtc=Base, ToUtc=Base.AddDays(30), Aggregation="avg", Bucket="15m",
            DeviceIds=Enumerable.Range(0,5).Select(_=>Guid.NewGuid()).ToArray(), MetricKeys=Enumerable.Range(0,5).Select(i=>$"m{i}").ToArray() };
        Assert.Equal("ESTIMATED_POINTS_EXCEEDS_LIMIT", Assert.Throws<TelemetryQueryValidationException>(
            ()=>QueryTelemetryUseCase.ValidateEstimatedPoints(request,25)).ErrorCode);
    }

    // telemetry_points is RANGE-partitioned on OccurredAtUtc, and the migration only creates
    // partitions from the previous month through six months ahead. A hard-coded date (this
    // was 2026-07-12) silently falls out of that window as time passes and every insert then
    // fails with "no partition of relation telemetry_points found for row". The suite never
    // caught it because it used to return early instead of running.
    //
    // Anchoring to the first day of the current month keeps every seeded point inside the
    // window forever, and leaves room for the widest span used here (~33 minutes).
    private static readonly DateTime Base = new(
        DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 12, 0, 0, DateTimeKind.Utc);
    private static TelemetryQueryRequest Request(Guid device, string metric) =>
        new([device],[metric],Base,Base.AddHours(1),"raw",null);

    private static QueryTelemetryUseCase UseCase(FluxoDbContext db) => new(Auth(db),
        new GetAuthorizedDevicesUseCase(new DeviceRepository(db)), new ResolveMetricDefinitionsUseCase(new TelemetryQueryRepository(db)),
        new TelemetryQueryRepository(db));
    private static GetAuthorizedWorkspaceUseCase Auth(FluxoDbContext db) => new(new WorkspaceRepository(db), new WorkspaceMembershipRepository(db));

    private static async Task<Seed> SeedAsync(FluxoDbContext db, bool member=true, string metricKey="metric", double value=1,
        int[]? pointOffsets=null)
    {
        var workspace=new Workspace($"t{Guid.NewGuid():N}"[..12],"Workspace"); var user=new PlatformUser($"u{Guid.NewGuid():N}@test.dev","hash","salt");
        var device=new Device(workspace.Id,"Device",$"dev-{Guid.NewGuid():N}",DeviceCategory.Sensor,tenantId:workspace.TenantId);
        var metric=new MetricDefinition(workspace.Id,workspace.TenantId,metricKey,MetricValueType.Numeric,DateTime.UtcNow);
        db.AddRange(workspace,user,device,metric); if(member) db.Add(new WorkspaceMembership(workspace.Id,user.Id,WorkspaceMembershipRole.Viewer));
        var offsets=pointOffsets??[5]; foreach(var offset in offsets) { var at=Base.AddSeconds(offset); var ingestion=new TelemetryIngestionRecord(workspace.TenantId,workspace.Id,device.Identifier,"telemetry","topic","2",at,at,"{}",sequence:offset+1); db.Add(ingestion); db.Add(new TelemetryPoint(workspace.TenantId,workspace.Id,device.Identifier,metric.Id,at,ingestion.Id,numeric:value)); }
        await db.SaveChangesAsync(); return new(workspace,user,device,metric);
    }
    private sealed record Seed(Workspace Workspace, PlatformUser User, Device Device, MetricDefinition Metric);

    private static FluxoDbContext Context(string cs)=>new(new DbContextOptionsBuilder<FluxoDbContext>().UseNpgsql(cs).Options);
    private static async Task WithDatabaseAsync(Func<FluxoDbContext,Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        await DisposableTestDatabase.WithDatabaseAsync("query", async cs =>
        {
            await using var db = Context(cs);
            await db.Database.MigrateAsync();
            await test(db);
        });
    }
}
