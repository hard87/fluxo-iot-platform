using System.Net;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Domain.Enums;

namespace Fluxo.IntegrationTests.Api;

public class TelemetryEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TelemetryEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_Telemetry_For_Existing_Device_Should_Return_Created()
    {
        var deviceId = await CreateDeviceAsync();
        var request = new CreateTelemetryRequest
        {
            DeviceId = deviceId,
            PayloadJson = "{\"temperature\":24.1}",
            OccurredAtUtc = DateTime.UtcNow.AddSeconds(-5)
        };

        var response = await _client.PostAsJsonAsync("/api/telemetry", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TelemetryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(deviceId, payload.DeviceId);
    }

    [Fact]
    public async Task Post_Telemetry_For_Missing_Device_Should_Return_NotFound()
    {
        var request = new CreateTelemetryRequest
        {
            DeviceId = Guid.NewGuid(),
            PayloadJson = "{\"pressure\":2.1}",
            OccurredAtUtc = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/telemetry", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Telemetry_Should_Return_In_Descending_Order_By_OccurredAtUtc()
    {
        var deviceId = await CreateDeviceAsync();

        await _client.PostAsJsonAsync("/api/telemetry", new CreateTelemetryRequest
        {
            DeviceId = deviceId,
            PayloadJson = "{\"sequence\":1}",
            OccurredAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        await _client.PostAsJsonAsync("/api/telemetry", new CreateTelemetryRequest
        {
            DeviceId = deviceId,
            PayloadJson = "{\"sequence\":2}",
            OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
        });

        var response = await _client.GetAsync($"/api/devices/{deviceId}/telemetry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var records = await response.Content.ReadFromJsonAsync<List<TelemetryResponse>>();
        Assert.NotNull(records);
        Assert.True(records.Count >= 2);
        Assert.True(records[0].OccurredAtUtc >= records[1].OccurredAtUtc);
    }

    private async Task<Guid> CreateDeviceAsync()
    {
        var request = new CreateDeviceRequest
        {
            WorkspaceId = Guid.NewGuid(),
            Name = "Line Sensor",
            Identifier = Guid.NewGuid().ToString("N"),
            Category = DeviceCategory.Sensor
        };

        var response = await _client.PostAsJsonAsync("/api/devices", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DeviceResponse>();
        return payload!.Id;
    }
}
