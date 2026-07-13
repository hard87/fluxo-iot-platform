using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.DTOs.Workspaces;
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
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();
        var deviceId = await CreateDeviceAsync(workspace.Id);

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
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();
        var deviceId = await CreateDeviceAsync(workspace.Id);

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

    private async Task<Guid> CreateDeviceAsync(Guid workspaceId)
    {
        var request = new CreateDeviceRequest
        {
            WorkspaceId = workspaceId,
            Name = "Line Sensor",
            Identifier = Guid.NewGuid().ToString("N"),
            Category = DeviceCategory.Sensor
        };

        var response = await _client.PostAsJsonAsync("/api/devices", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DeviceResponse>();
        return payload!.Id;
    }

    private async Task<WorkspaceResponse> CreateWorkspaceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = $"Telemetry Workspace {Guid.NewGuid():N}"
        });

        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!;
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"telemetry-user-{Guid.NewGuid():N}@fluxo.local";
        const string password = "Abcdef!23456";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Email = email,
            Password = password
        });
        register.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.EnsureSuccessStatusCode();

        var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }
}
