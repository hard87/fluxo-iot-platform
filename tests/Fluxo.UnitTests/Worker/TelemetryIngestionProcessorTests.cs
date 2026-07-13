using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fluxo.Domain.Models;
using System.Text.Json;
using System.Diagnostics.Metrics;
using Fluxo.Domain.Exceptions;

namespace Fluxo.UnitTests.Worker;

public class TelemetryIngestionProcessorTests
{
    private static readonly ILogger<TelemetryIngestionProcessor> Logger =
        LoggerFactory.Create(_ => { }).CreateLogger<TelemetryIngestionProcessor>();

    [Fact]
    public async Task Should_Persist_When_Payload_Is_Valid_And_Unique()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository
        {
            AddResult = TelemetryIngestionWriteResult.Persisted
        };

        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var deviceRepository = CreateProvisionedDeviceRepository();
        var processor = CreateProcessor(ingestionRepository, rejectionRepository, deviceRepository);

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "esp32-lab-01",
            10);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted, result.Status);
        Assert.Single(ingestionRepository.Records);
        Assert.Empty(rejectionRepository.Records);
        Assert.NotNull(deviceRepository.CurrentDevice.LastContactAtUtc);
    }

    [Fact]
    public async Task Should_Reject_When_Payload_Is_Invalid_Json()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository();
        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var processor = CreateProcessor(ingestionRepository, rejectionRepository);

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";

        var result = await processor.ProcessAsync(topic, "{invalid", DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.PayloadInvalid, rejectionRepository.Records[0].ErrorType);
    }

    [Fact]
    public async Task Should_Reject_When_Topic_Device_Differs_From_Payload_Device()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository();
        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var processor = CreateProcessor(ingestionRepository, rejectionRepository);

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/device-a/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "device-b",
            1);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.Validation, rejectionRepository.Records[0].ErrorType);
        Assert.Contains("deviceId do payload difere do topic", rejectionRepository.Records[0].Reason);
    }

    [Fact]
    public async Task Should_Discard_Duplicate_Message()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository
        {
            AddResult = TelemetryIngestionWriteResult.Duplicate
        };

        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var deviceRepository = CreateProvisionedDeviceRepository();
        var processor = CreateProcessor(ingestionRepository, rejectionRepository, deviceRepository);

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "esp32-lab-01",
            44);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Duplicate, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.Duplicate, rejectionRepository.Records[0].ErrorType);
    }

    [Fact]
    public async Task Should_Classify_Transient_Database_Failure_After_Retries()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository
        {
            ExceptionToThrow = new TimeoutException("simulated timeout")
        };

        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var deviceRepository = CreateProvisionedDeviceRepository();
        var processor = CreateProcessor(
            ingestionRepository,
            rejectionRepository,
            deviceRepository,
            new MqttIngestionOptions
            {
                DatabaseRetryCount = 1,
                DatabaseRetryDelayMs = 1
            });

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "esp32-lab-01",
            9);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.TransientFailure, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.TransientError, rejectionRepository.Records[0].ErrorType);
    }

    [Fact]
    public async Task Should_Reject_When_Device_Is_Not_Provisioned()
    {
        var ingestionRepository = new FakeTelemetryIngestionRepository();
        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var processor = CreateProcessor(
            ingestionRepository,
            rejectionRepository,
            new FakeDeviceRepository(
                new Device(
                    Guid.NewGuid(),
                    "Other Device",
                    "other-device",
                    DeviceCategory.Sensor,
                    tenantId: "other-tenant")));

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "esp32-lab-01",
            45);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.Validation, rejectionRepository.Records[0].ErrorType);
        Assert.Contains("Dispositivo nao provisionado", rejectionRepository.Records[0].Reason);
    }

    [Fact]
    public async Task Should_Reject_When_Device_Is_Inactive()
    {
        var inactiveDevice = new Device(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "ESP32 Inactive",
            "esp32-lab-01",
            DeviceCategory.Sensor,
            tenantId: "acme");
        inactiveDevice.Deactivate();

        var ingestionRepository = new FakeTelemetryIngestionRepository();
        var rejectionRepository = new FakeTelemetryIngestionRejectionRepository();
        var processor = CreateProcessor(
            ingestionRepository,
            rejectionRepository,
            new FakeDeviceRepository(inactiveDevice));

        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        var payload = BuildPayload(
            "acme",
            "11111111-1111-1111-1111-111111111111",
            "esp32-lab-01",
            46);

        var result = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Status);
        Assert.Single(rejectionRepository.Records);
        Assert.Equal(TelemetryIngestionFailureType.Validation, rejectionRepository.Records[0].ErrorType);
        Assert.Contains("Dispositivo inativo", rejectionRepository.Records[0].Reason);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"bad key\":1}")]
    [InlineData("{\"Temperature\":1}")]
    [InlineData("{\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\":1}")]
    [InlineData("{\"array\":[1]}")]
    [InlineData("{\"object\":{\"x\":1}}")]
    [InlineData("{\"null_value\":null}")]
    public async Task V2_InvalidMetricContracts_AreRejected(string metricsJson)
    {
        var result = await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{metricsJson}}}");
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Result.Status);
        Assert.Empty(result.Repository.Metrics);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("+Infinity")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task V2_NonFiniteNumericValues_AreRejectedExplicitly(string numericLiteral)
    {
        var result = await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"value\":{numericLiteral}}}}}");

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Result.Status);
        Assert.Empty(result.Repository.Metrics);
        Assert.Single(result.Rejections.Records);
        Assert.Equal(TelemetryIngestionFailureType.PayloadInvalid, result.Rejections.Records[0].ErrorType);
    }

    [Fact]
    public async Task V2_MissingSchemaVersion_IsRejectedExplicitly()
    {
        var result = await ProcessV2Async($"{{\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"value\":1}}}}");

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Result.Status);
        Assert.Empty(result.Repository.Metrics);
        Assert.Single(result.Rejections.Records);
        Assert.Contains("schemaVersion ausente", result.Rejections.Records[0].Reason);
    }

    [Fact]
    public async Task V2_UnknownSchemaVersion_IsRejectedExplicitly()
    {
        var result = await ProcessV2Async($"{{\"schemaVersion\":3,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"value\":1}}}}");

        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Result.Status);
        Assert.Empty(result.Repository.Metrics);
        Assert.Single(result.Rejections.Records);
        Assert.Contains("schemaVersion desconhecida", result.Rejections.Records[0].Reason);
    }

    [Theory]
    [InlineData("\"short_text\":\"ok\"")]
    [InlineData("\"numeric\":12.5")]
    [InlineData("\"boolean\":true")]
    [InlineData("\"numeric\":12.5,\"boolean\":true,\"text\":\"running\"")]
    public async Task V2_ScalarContracts_AreAccepted(string metrics)
    {
        var result = await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{{metrics}}}}}");
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted, result.Result.Status);
        Assert.NotEmpty(result.Repository.Metrics);
    }

    [Fact]
    public async Task V2_MetricCountAndTextBoundaries_AreEnforced()
    {
        var sixtyFour = string.Join(',', Enumerable.Range(0, 64).Select(i => $"\"m{i}\":{i}"));
        Assert.Equal(64, (await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{{sixtyFour}}}}}")).Repository.Metrics.Count);
        var sixtyFive = sixtyFour + ",\"overflow\":65";
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected,
            (await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{{sixtyFive}}}}}")).Result.Status);
        var text256 = new string('x', 256); var text257 = new string('x', 257);
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted,
            (await ProcessV2Async(JsonSerializer.Serialize(new { schemaVersion=2, sequence=1, occurredAtUtc=DateTime.UtcNow, metrics=new Dictionary<string,object>{{"text",text256}} }))).Result.Status);
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected,
            (await ProcessV2Async(JsonSerializer.Serialize(new { schemaVersion=2, sequence=1, occurredAtUtc=DateTime.UtcNow, metrics=new Dictionary<string,object>{{"text",text257}} }))).Result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task V2_NonPositiveSequence_IsRejected(long sequence)
    {
        var result = await ProcessV2Async($"{{\"schemaVersion\":2,\"sequence\":{sequence},\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"value\":1}}}}");
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, result.Result.Status);
    }

    [Fact]
    public async Task V2_TimestampWindows_AreEnforced()
    {
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted,
            (await ProcessV2Async(V2At(DateTime.UtcNow.AddMinutes(4)))).Result.Status);
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected,
            (await ProcessV2Async(V2At(DateTime.UtcNow.AddMinutes(6)))).Result.Status);
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted,
            (await ProcessV2Async(V2At(DateTime.UtcNow.AddDays(-29)))).Result.Status);
        Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected,
            (await ProcessV2Async(V2At(DateTime.UtcNow.AddDays(-31)))).Result.Status);
    }

    [Fact]
    public async Task CardinalityGuardCounter_IncrementsExactlyOncePerRejectedMessage()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        { if (instrument.Name == "metric_cardinality_guard_triggered_total") meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref count, value)); listener.Start();
        using var metrics = new IngestionMetrics();
        var repository = new FakeTelemetryIngestionRepository { ExceptionToThrow = new MetricCardinalityGuardException("new_key") };
        var processor = new TelemetryIngestionProcessor(repository, new FakeTelemetryIngestionRejectionRepository(),
            CreateProvisionedDeviceRepository(), Options.Create(new MqttIngestionOptions { DatabaseRetryCount = 1 }), Logger, metrics);
        var payload = $"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"new_key\":1,\"another_key\":2}}}}";
        await processor.ProcessAsync("fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry", payload, DateTime.UtcNow);
        Assert.Equal(1, count);
    }

    private static string V2At(DateTime timestamp) => $"{{\"schemaVersion\":2,\"sequence\":1,\"occurredAtUtc\":\"{timestamp:O}\",\"metrics\":{{\"value\":1}}}}";
    private static async Task<(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingResult Result, FakeTelemetryIngestionRepository Repository, FakeTelemetryIngestionRejectionRepository Rejections)> ProcessV2Async(string payload)
    {
        var repository = new FakeTelemetryIngestionRepository(); var rejection = new FakeTelemetryIngestionRejectionRepository();
        var processor = CreateProcessor(repository, rejection);
        var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
        return (await processor.ProcessAsync(topic, payload, DateTime.UtcNow), repository, rejection);
    }

    private static TelemetryIngestionProcessor CreateProcessor(
        FakeTelemetryIngestionRepository ingestionRepository,
        FakeTelemetryIngestionRejectionRepository rejectionRepository,
        FakeDeviceRepository? deviceRepository = null,
        MqttIngestionOptions? options = null)
    {
        return new TelemetryIngestionProcessor(
            ingestionRepository,
            rejectionRepository,
            deviceRepository ?? CreateProvisionedDeviceRepository(),
            Options.Create(options ?? new MqttIngestionOptions()),
            Logger);
    }

    private static FakeDeviceRepository CreateProvisionedDeviceRepository()
    {
        return new FakeDeviceRepository(
            new Device(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "ESP32 Lab",
                "esp32-lab-01",
                DeviceCategory.Sensor,
                tenantId: "acme"));
    }

    private static string BuildPayload(
        string tenantId,
        string workspaceId,
        string deviceId,
        long sequence)
    {
        return $"{{\"schemaVersion\":\"1.0\",\"tenantId\":\"{tenantId}\",\"workspaceId\":\"{workspaceId}\",\"deviceId\":\"{deviceId}\",\"messageType\":\"telemetry\",\"timestampUtc\":\"{DateTime.UtcNow:O}\",\"sequence\":{sequence},\"metrics\":{{\"temperature\":25.1}}}}";
    }

    private sealed class FakeTelemetryIngestionRepository : ITelemetryIngestionRepository
    {
        public List<TelemetryIngestionRecord> Records { get; } = [];
        public List<TelemetryMetricValue> Metrics { get; } = [];
        public TelemetryIngestionWriteResult AddResult { get; set; } = TelemetryIngestionWriteResult.Persisted;
        public Exception? ExceptionToThrow { get; set; }

        public Task<TelemetryIngestionWriteResult> AddAsync(
            TelemetryIngestionRecord telemetry,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            Records.Add(telemetry);
            return Task.FromResult(AddResult);
        }
        public Task<TelemetryIngestionWriteResult> AddWithPointsAsync(TelemetryIngestionRecord telemetry, IReadOnlyList<TelemetryMetricValue> metrics, CancellationToken cancellationToken = default)
        { Metrics.AddRange(metrics); return AddAsync(telemetry, cancellationToken); }

        public Task<IReadOnlyList<TelemetryIngestionRecord>> GetByWorkspaceAndDeviceAsync(
            Guid workspaceId,
            string deviceId,
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TelemetryIngestionRecord>>(Records);
        }

        public Task<long> CountByTenantWorkspaceAsync(
            string tenantId,
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((long)Records.Count);
        }
    }

    private sealed class FakeTelemetryIngestionRejectionRepository : ITelemetryIngestionRejectionRepository
    {
        public List<TelemetryIngestionRejectionRecord> Records { get; } = [];

        public Task AddAsync(
            TelemetryIngestionRejectionRecord rejection,
            CancellationToken cancellationToken = default)
        {
            Records.Add(rejection);
            return Task.CompletedTask;
        }

        public Task<long> CountByTenantWorkspaceAsync(
            string tenantId,
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((long)Records.Count);
        }

        public Task<IReadOnlyList<TelemetryIngestionRejectionRecord>> GetReprocessableBatchAsync(
            IReadOnlyCollection<TelemetryIngestionFailureType> errorTypes,
            int maxAttempts,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var batch = Records
                .Where(x =>
                    x.SourceRejectionId is null &&
                    !x.Reprocessed &&
                    x.ReprocessAttempts < maxAttempts &&
                    errorTypes.Contains(x.ErrorType))
                .OrderBy(x => x.ReceivedAtUtc)
                .Take(batchSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<TelemetryIngestionRejectionRecord>>(batch);
        }

        public Task RecordReprocessAttemptAsync(
            Guid rejectionId,
            DateTime attemptedAtUtc,
            bool resolved,
            CancellationToken cancellationToken = default)
        {
            var rejection = Records.FirstOrDefault(x => x.Id == rejectionId);
            rejection?.RecordReprocessAttempt(attemptedAtUtc, resolved);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeviceRepository : IDeviceRepository
    {
        public FakeDeviceRepository(Device device)
        {
            CurrentDevice = device;
        }

        public Device CurrentDevice { get; }

        public Task AddAsync(Device device, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Device?>(CurrentDevice.Id == id ? CurrentDevice : null);
        }

        public Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Device?>(CurrentDevice.Id == id ? CurrentDevice : null);
        }

        public Task<Device?> GetByWorkspaceAndIdentifierAsync(
            Guid workspaceId,
            string identifier,
            CancellationToken cancellationToken = default)
        {
            var found = CurrentDevice.WorkspaceId == workspaceId &&
                        CurrentDevice.Identifier == identifier.Trim() &&
                        CurrentDevice.TenantId == Fluxo.Domain.Entities.Device.DefaultTenantId;

            return Task.FromResult<Device?>(found ? CurrentDevice : null);
        }

        public Task<Device?> GetByTenantWorkspaceAndIdentifierAsync(
            string tenantId,
            Guid workspaceId,
            string identifier,
            CancellationToken cancellationToken = default)
        {
            var found = CurrentDevice.TenantId == tenantId.Trim() &&
                        CurrentDevice.WorkspaceId == workspaceId &&
                        CurrentDevice.Identifier == identifier.Trim();

            return Task.FromResult<Device?>(found ? CurrentDevice : null);
        }

        public Task<Device?> GetTrackedByTenantWorkspaceAndIdentifierAsync(
            string tenantId,
            Guid workspaceId,
            string identifier,
            CancellationToken cancellationToken = default)
        {
            return GetByTenantWorkspaceAndIdentifierAsync(tenantId, workspaceId, identifier, cancellationToken);
        }

        public Task<Device?> GetByTenantWorkspaceAndIdAsync(
            string tenantId,
            Guid workspaceId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            var found = CurrentDevice.TenantId == tenantId.Trim() &&
                        CurrentDevice.WorkspaceId == workspaceId &&
                        CurrentDevice.Id == deviceId;

            return Task.FromResult<Device?>(found ? CurrentDevice : null);
        }

        public Task<IReadOnlyList<Device>> GetAllByWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            var devices = CurrentDevice.WorkspaceId == workspaceId
                ? new[] { CurrentDevice }
                : Array.Empty<Device>();

            return Task.FromResult<IReadOnlyList<Device>>(devices);
        }

        public Task<IReadOnlyList<Device>> GetAllByTenantWorkspaceAsync(
            string tenantId,
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            var devices = CurrentDevice.WorkspaceId == workspaceId &&
                          CurrentDevice.TenantId == tenantId.Trim()
                ? new[] { CurrentDevice }
                : Array.Empty<Device>();

            return Task.FromResult<IReadOnlyList<Device>>(devices);
        }

        public Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
