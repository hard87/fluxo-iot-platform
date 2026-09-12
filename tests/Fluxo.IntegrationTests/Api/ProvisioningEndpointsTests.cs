using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Domain.Enums;
using Fluxo.IntegrationTests.Infrastructure;

namespace Fluxo.IntegrationTests.Api;

public class ProvisioningEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProvisioningEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Provision_Device_Should_Create_Device_And_Credential()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();

        var request = new ProvisionDeviceRequest
        {
            TenantId = workspace.TenantId,
            WorkspaceId = workspace.Id,
            Name = "ESP32 Lab 01",
            Identifier = "esp32-lab-01",
            Category = DeviceCategory.Sensor,
            MetadataJson = "{\"line\":\"A\"}"
        };

        var response = await _client.PostAsJsonAsync("/api/provisioning/devices", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProvisionedDeviceResponse>(TestJsonOptions.Default);

        Assert.NotNull(payload);
        Assert.Equal(request.TenantId, payload.TenantId);
        Assert.Equal(request.WorkspaceId, payload.WorkspaceId);
        Assert.Equal(request.Identifier, payload.DeviceIdentifier);
        Assert.False(string.IsNullOrWhiteSpace(payload.CredentialUsername));
        Assert.False(string.IsNullOrWhiteSpace(payload.ProvisioningSecret));
        Assert.Contains("/devices/esp32-lab-01/telemetry", payload.MqttPublishTopic);
    }

    [Fact]
    public async Task Rotate_Credential_Should_Create_New_Active_Credential()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();

        var provision = await _client.PostAsJsonAsync("/api/provisioning/devices", new ProvisionDeviceRequest
        {
            TenantId = workspace.TenantId,
            WorkspaceId = workspace.Id,
            Name = "ESP32 Lab 02",
            Identifier = "esp32-lab-02",
            Category = DeviceCategory.Sensor
        });

        provision.EnsureSuccessStatusCode();
        var provisioned = await provision.Content.ReadFromJsonAsync<ProvisionedDeviceResponse>(TestJsonOptions.Default);
        Assert.NotNull(provisioned);

        var rotate = await _client.PostAsync(
            $"/api/provisioning/devices/{provisioned.DeviceId}/credentials/rotate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var rotated = await rotate.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>();

        Assert.NotNull(rotated);
        Assert.NotEqual(provisioned.CredentialId, rotated.CredentialId);
        Assert.False(string.IsNullOrWhiteSpace(rotated.ProvisioningSecret));
        Assert.Equal(DeviceCredentialStatus.Active, rotated.CredentialStatus);
    }

    private async Task<WorkspaceResponse> CreateWorkspaceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = $"Provisioning Workspace {Guid.NewGuid():N}"
        });

        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!;
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"provisioning-user-{Guid.NewGuid():N}@fluxo.local";
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
