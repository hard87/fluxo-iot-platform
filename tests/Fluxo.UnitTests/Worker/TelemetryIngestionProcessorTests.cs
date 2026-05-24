using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        var processor = CreateProcessor(ingestionRepository, rejectionRepository);

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
        var processor = CreateProcessor(ingestionRepository, rejectionRepository);

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
        var processor = CreateProcessor(
            ingestionRepository,
            rejectionRepository,
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

    private static TelemetryIngestionProcessor CreateProcessor(
        FakeTelemetryIngestionRepository ingestionRepository,
        FakeTelemetryIngestionRejectionRepository rejectionRepository,
        MqttIngestionOptions? options = null)
    {
        return new TelemetryIngestionProcessor(
            ingestionRepository,
            rejectionRepository,
            Options.Create(options ?? new MqttIngestionOptions()),
            Logger);
    }

    private static string BuildPayload(
        string tenantId,
        string workspaceId,
        string deviceId,
        long sequence)
    {
        return $"{{\"schemaVersion\":\"1.0\",\"tenantId\":\"{tenantId}\",\"workspaceId\":\"{workspaceId}\",\"deviceId\":\"{deviceId}\",\"messageType\":\"telemetry\",\"timestampUtc\":\"2026-05-24T12:00:00Z\",\"sequence\":{sequence},\"metrics\":{{\"temperature\":25.1}}}}";
    }

    private sealed class FakeTelemetryIngestionRepository : ITelemetryIngestionRepository
    {
        public List<TelemetryIngestionRecord> Records { get; } = [];
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

        public Task<IReadOnlyList<TelemetryIngestionRecord>> GetByWorkspaceAndDeviceAsync(
            Guid workspaceId,
            string deviceId,
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TelemetryIngestionRecord>>(Records);
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
    }
}
