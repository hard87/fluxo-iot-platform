using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Repositories;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fluxo.IntegrationTests.Ingestion;

public class TelemetryIngestionPostgreSqlTests
{
    private static readonly ILogger<TelemetryIngestionProcessor> Logger =
        LoggerFactory.Create(_ => { }).CreateLogger<TelemetryIngestionProcessor>();

    [Fact]
    public async Task Processor_Should_Persist_Handle_Duplicate_And_Record_Rejections_On_PostgreSql()
    {
        var adminConnection = ResolveAdminConnectionString();
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        var databaseName = $"fluxo_it_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection);
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = databaseName
        };

        await CreateDatabaseAsync(adminBuilder.ConnectionString, databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<FluxoDbContext>()
                .UseNpgsql(testBuilder.ConnectionString)
                .Options;

            await using var context = new FluxoDbContext(options);
            await context.Database.MigrateAsync();

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            Assert.Contains("AddDeviceProvisioningAndOperationalStatus", appliedMigrations);

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
            var invalidJsonResult = await processor.ProcessAsync(topic, "{invalid", DateTime.UtcNow.AddSeconds(2));

            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Persisted, firstResult.Status);
            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Duplicate, duplicateResult.Status);
            Assert.Equal(Fluxo.Worker.Ingestion.Models.TelemetryIngestionProcessingStatus.Rejected, invalidJsonResult.Status);

            var storedTelemetryCount = await context.TelemetryIngestionRecords.CountAsync();
            Assert.Equal(1, storedTelemetryCount);

            var rejectionTypes = await context.TelemetryIngestionRejectionRecords
                .Select(x => x.ErrorType)
                .ToListAsync();

            Assert.Contains(TelemetryIngestionFailureType.Duplicate, rejectionTypes);
            Assert.Contains(TelemetryIngestionFailureType.PayloadInvalid, rejectionTypes);

            var refreshedDevice = await context.Devices.AsNoTracking().SingleAsync(x => x.Id == device.Id);
            Assert.NotNull(refreshedDevice.LastContactAtUtc);
            Assert.Equal(100, refreshedDevice.LastTelemetrySequence);
        }
        finally
        {
            await DropDatabaseAsync(adminBuilder.ConnectionString, databaseName);
        }
    }

    private static async Task CreateDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var terminateCommand = connection.CreateCommand();
        terminateCommand.CommandText = $@"
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '{databaseName}'
  AND pid <> pg_backend_pid();";
        await terminateCommand.ExecuteNonQueryAsync();

        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private static string? ResolveAdminConnectionString()
    {
        return Environment.GetEnvironmentVariable("FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION");
    }

    private static string BuildPayload(string deviceId, long sequence)
    {
        return $"{{\"schemaVersion\":\"1.0\",\"tenantId\":\"acme\",\"workspaceId\":\"11111111-1111-1111-1111-111111111111\",\"deviceId\":\"{deviceId}\",\"messageType\":\"telemetry\",\"timestampUtc\":\"2026-05-24T12:00:00Z\",\"sequence\":{sequence},\"metrics\":{{\"temperature\":25.1}}}}";
    }
}
