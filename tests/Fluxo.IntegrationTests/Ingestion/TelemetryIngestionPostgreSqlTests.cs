using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.IntegrationTests.Infrastructure;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxo.IntegrationTests.Ingestion;

public class TelemetryIngestionPostgreSqlTests
{
    private static readonly ILogger<TelemetryIngestionProcessor> Logger =
        LoggerFactory.Create(_ => { }).CreateLogger<TelemetryIngestionProcessor>();

    [SkippableFact]
    public async Task Processor_Should_Persist_Handle_Duplicate_And_Record_Rejections_On_PostgreSql()
    {
        DisposableTestDatabase.SkipUnlessAvailable();

        await DisposableTestDatabase.WithDatabaseAsync("it", async connectionString =>
        {
            var options = new DbContextOptionsBuilder<FluxoDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using var context = new FluxoDbContext(options);
            await context.Database.MigrateAsync();

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            Assert.Contains(appliedMigrations, x => x.EndsWith("AddDeviceProvisioningAndOperationalStatus", StringComparison.Ordinal));

            var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var device = new Device(
                workspaceId,
                "ESP32 Lab",
                "esp32-lab-01",
                DeviceCategory.Sensor,
                tenantId: "acme");

            context.Devices.Add(device);
            await context.SaveChangesAsync();

            var ingestionRepository = new TelemetryIngestionRepository(context);
            var rejectionRepository = new TelemetryIngestionRejectionRepository(context);
            var deviceRepository = new DeviceRepository(context);

            var processor = new TelemetryIngestionProcessor(
                ingestionRepository,
                rejectionRepository,
                deviceRepository,
                Options.Create(new MqttIngestionOptions
                {
                    DatabaseRetryCount = 1,
                    DatabaseRetryDelayMs = 1
                }),
                Logger);

            var topic = "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry";
            var payload = BuildPayload("esp32-lab-01", 100);

            var firstResult = await processor.ProcessAsync(topic, payload, DateTime.UtcNow);
            var duplicateResult = await processor.ProcessAsync(topic, payload, DateTime.UtcNow.AddSeconds(1));
            var v2Payload = $"{{\"schemaVersion\":2,\"sequence\":101,\"occurredAtUtc\":\"{DateTime.UtcNow:O}\",\"metrics\":{{\"temperature_c\":24.5,\"door_open\":true,\"machine_state\":\"running\"}}}}";
            var v2Result = await processor.ProcessAsync(topic, v2Payload, DateTime.UtcNow.AddSeconds(1));
            var invalidJsonResult = await processor.ProcessAsync(topic, "{invalid", DateTime.UtcNow.AddSeconds(2));

            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted, firstResult.Status);
            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Duplicate, duplicateResult.Status);
            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted, v2Result.Status);
            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, invalidJsonResult.Status);

            var storedTelemetryCount = await context.TelemetryIngestionRecords.CountAsync();
            Assert.Equal(2, storedTelemetryCount);
            Assert.Equal(4, await context.TelemetryPoints.CountAsync());
            Assert.Equal(4, await context.MetricDefinitions.CountAsync());
            Assert.Contains(await context.TelemetryPoints.ToListAsync(), x => x.BooleanValue == true);
            Assert.Contains(await context.TelemetryPoints.ToListAsync(), x => x.TextValue == "running");

            var rejectionTypes = await context.TelemetryIngestionRejectionRecords
                .Select(x => x.ErrorType)
                .ToListAsync();

            Assert.Contains(TelemetryIngestionFailureType.Duplicate, rejectionTypes);
            Assert.Contains(TelemetryIngestionFailureType.PayloadInvalid, rejectionTypes);

            var refreshedDevice = await context.Devices.AsNoTracking().SingleAsync(x => x.Id == device.Id);
            Assert.NotNull(refreshedDevice.LastContactAtUtc);
            Assert.Equal(101, refreshedDevice.LastTelemetrySequence);
        });
    }

    private static string BuildPayload(string deviceId, long sequence)
    {
        return $"{{\"schemaVersion\":\"1.0\",\"tenantId\":\"acme\",\"workspaceId\":\"11111111-1111-1111-1111-111111111111\",\"deviceId\":\"{deviceId}\",\"messageType\":\"telemetry\",\"timestampUtc\":\"{DateTime.UtcNow:O}\",\"sequence\":{sequence},\"metrics\":{{\"temperature\":25.1}}}}";
    }
}
