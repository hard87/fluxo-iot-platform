using Fluxo.Application.Alerts;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Alerts;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Alerts;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Fluxo.Worker.Ingestion.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fluxo.IntegrationTests.Alerts;

/// <summary>
/// Hosts the real <see cref="AlertEvaluationWorker"/> -- its actual BackgroundService poll loop,
/// not <c>AlertBackendTests.Scenario.Drain</c>'s manual claim/evaluate loop -- against a
/// disposable Postgres database. This is what would have caught AlertEvaluation:Enabled shipping
/// unset (worker silently inert in every environment) before it reached production.
/// </summary>
internal sealed class AlertWorkerHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AlertEvaluationWorker _worker;

    public string ConnectionString { get; }
    public Workspace Workspace { get; }
    public PlatformUser User { get; }
    public Device Device { get; }
    public MetricDefinition Metric { get; }

    private AlertWorkerHarness(string connectionString, Workspace workspace, PlatformUser user,
        Device device, MetricDefinition metric, ServiceProvider provider, AlertEvaluationWorker worker)
    {
        ConnectionString = connectionString;
        Workspace = workspace;
        User = user;
        Device = device;
        Metric = metric;
        _provider = provider;
        _worker = worker;
    }

    public static async Task<AlertWorkerHarness> StartAsync(string connectionString, bool enabled = true,
        int pollIntervalMilliseconds = 50, string deviceIdentifier = "sensor-1", string metricKey = "temperature")
    {
        var workspace = new Workspace("alerts-e2e", "Alerts E2E");
        var user = new PlatformUser($"owner-{Guid.NewGuid():N}@example.test", "hash", "salt");
        var device = new Device(workspace.Id, "Sensor", deviceIdentifier, DeviceCategory.Sensor, tenantId: workspace.TenantId);
        var metric = new MetricDefinition(workspace.Id, workspace.TenantId, metricKey, MetricValueType.Numeric, DateTime.UtcNow);

        await using (var seed = new FluxoDbContext(new DbContextOptionsBuilder<FluxoDbContext>().UseNpgsql(connectionString).Options))
        {
            await seed.Database.MigrateAsync();
            seed.AddRange(workspace, user, device, metric, new WorkspaceMembership(workspace.Id, user.Id, WorkspaceMembershipRole.Owner));
            await seed.SaveChangesAsync();
        }

        var worker = BuildWorker(connectionString, enabled, pollIntervalMilliseconds, out var provider);
        var harness = new AlertWorkerHarness(connectionString, workspace, user, device, metric, provider, worker);
        await ((IHostedService)worker).StartAsync(CancellationToken.None);
        return harness;
    }

    /// <summary>
    /// Starts an additional real worker instance against an existing environment's database,
    /// without reseeding workspace/device/metric -- used to prove two live worker instances
    /// claiming concurrently behave correctly (SKIP LOCKED, no duplicate events).
    /// </summary>
    public static Task<AlertWorkerInstance> StartAdditionalWorkerAsync(string connectionString,
        bool enabled = true, int pollIntervalMilliseconds = 50)
    {
        var worker = BuildWorker(connectionString, enabled, pollIntervalMilliseconds, out var provider);
        return StartInstanceAsync(worker, provider);
    }

    private static async Task<AlertWorkerInstance> StartInstanceAsync(AlertEvaluationWorker worker, ServiceProvider provider)
    {
        await ((IHostedService)worker).StartAsync(CancellationToken.None);
        return new AlertWorkerInstance(worker, provider);
    }

    private static AlertEvaluationWorker BuildWorker(string connectionString, bool enabled,
        int pollIntervalMilliseconds, out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FluxoDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<AlertEvaluationEngine>();
        services.AddSingleton(Options.Create(new AlertEvaluationOptions
        {
            Enabled = enabled,
            PollIntervalMilliseconds = pollIntervalMilliseconds,
        }));
        services.AddLogging();
        provider = services.BuildServiceProvider();

        return new AlertEvaluationWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<AlertEvaluationOptions>>(),
            provider.GetRequiredService<ILogger<AlertEvaluationWorker>>());
    }

    public FluxoDbContext Db() => new(new DbContextOptionsBuilder<FluxoDbContext>().UseNpgsql(ConnectionString).Options);

    public AlertManagement Management(FluxoDbContext db) => new(db,
        new GetAuthorizedWorkspaceUseCase(new WorkspaceRepository(db), new WorkspaceMembershipRepository(db)),
        Options.Create(new AlertEvaluationOptions()));

    public SaveAlertRuleRequest Request(string operatorName = "GreaterThan", double? threshold = 80,
        double hysteresis = 2, int durationSeconds = 0, string? deviceIdentifier = null) =>
        new("E2E rule", Metric.Id, deviceIdentifier ?? Device.Identifier, operatorName, threshold,
            Hysteresis: hysteresis, DurationSeconds: durationSeconds);

    public async Task<AlertRuleRevision> CreateRuleAsync(SaveAlertRuleRequest? request = null)
    {
        await using var db = Db();
        var management = Management(db);
        var input = request ?? Request();
        var draft = await management.SaveAsync(User.Id, Workspace.Id, null, input with { Enabled = false }, default);
        return await management.SaveAsync(User.Id, Workspace.Id, draft.RuleId,
            input with { Enabled = true, ExpectedVersion = draft.Version }, default);
    }

    /// <summary>Adds a second device to the same workspace -- used to prove a global rule
    /// (DeviceIdentifier=null) keeps independent state per device.</summary>
    public async Task<Device> AddDeviceAsync(string identifier)
    {
        var device = new Device(Workspace.Id, "Sensor", identifier, DeviceCategory.Sensor, tenantId: Workspace.TenantId);
        await using var db = Db();
        db.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    /// <summary>Adds another active member to the workspace -- used as a portal-notification recipient.</summary>
    public async Task<PlatformUser> AddMemberAsync(WorkspaceMembershipRole role = WorkspaceMembershipRole.Viewer)
    {
        var member = new PlatformUser($"member-{Guid.NewGuid():N}@example.test", "hash", "salt");
        await using var db = Db();
        db.Add(member);
        db.Add(new WorkspaceMembership(Workspace.Id, member.Id, role));
        await db.SaveChangesAsync();
        return member;
    }

    public async Task PublishTelemetryAsync(long sequence, DateTime occurredAtUtc, object value,
        string? metricKey = null, string? deviceIdentifier = null)
    {
        await using var db = Db();
        var processor = new TelemetryIngestionProcessor(
            new TelemetryIngestionRepository(db, new NpgsqlBinaryCopyTelemetryPointWriter(db)),
            new TelemetryIngestionRejectionRepository(db), new DeviceRepository(db),
            Options.Create(new MqttIngestionOptions { DatabaseRetryCount = 1 }),
            NullLogger<TelemetryIngestionProcessor>.Instance);
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            sequence,
            occurredAtUtc,
            metrics = new Dictionary<string, object> { [metricKey ?? "temperature"] = value },
        });
        await processor.ProcessAsync(
            $"fluxo/tenants/{Workspace.TenantId}/workspaces/{Workspace.Id}/devices/{deviceIdentifier ?? Device.Identifier}/telemetry",
            payload, DateTime.UtcNow);
    }

    /// <summary>
    /// Polls <paramref name="predicate"/> against a fresh <see cref="FluxoDbContext"/> until it
    /// returns true or <paramref name="timeout"/> elapses -- the async, real-worker-timing
    /// equivalent of <c>AlertBackendTests.Scenario.Drain</c>'s synchronous manual loop.
    /// </summary>
    public async Task WaitForAsync(Func<FluxoDbContext, Task<bool>> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            await using var db = Db();
            if (await predicate(db)) return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met by the real AlertEvaluationWorker within the timeout.");
    }

    public async ValueTask DisposeAsync()
    {
        await ((IHostedService)_worker).StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
    }
}

/// <summary>A second, independently started real worker instance sharing another harness's database.</summary>
internal sealed class AlertWorkerInstance(AlertEvaluationWorker worker, ServiceProvider provider) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await ((IHostedService)worker).StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }
}
