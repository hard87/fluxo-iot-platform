using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Workspaces;

namespace Fluxo.IntegrationTests.Api;

/// <summary>
/// Guards the exact wire format the browser sends, as opposed to what a strongly-typed C#
/// client would produce. A test that builds <c>ProvisionDeviceRequest { Category = ... }</c>
/// and posts it with <c>PostAsJsonAsync</c> never reproduces this: System.Text.Json serializes
/// that enum member as a number regardless of what the API's input formatter actually accepts,
/// so a request/response enum-format mismatch (like the DeviceCategory string/number bug fixed
/// alongside this file) is invisible to that style of test. These tests send raw JSON strings
/// instead, mirroring portal-web's actual fetch() payloads.
/// </summary>
public class JsonContractTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JsonContractTests(FluxoWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("Sensor")]
    [InlineData("Actuator")]
    [InlineData("Gateway")]
    public async Task Provision_Device_As_Portal_Sends_It_Should_Return_Created(string category)
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspaceId = await CreateWorkspaceAsync();

        // Mirrors portal-web/src/services/api/deviceService.ts#provisionDevice: category is a
        // string literal from a <select>, never a C# enum member.
        var body = $$"""
            {
              "name": "Portal Device",
              "identifier": "portal-device-{{Guid.NewGuid():N}}",
              "category": "{{category}}"
            }
            """;

        var response = await _client.PostAsync(
            $"/api/workspaces/{workspaceId}/devices/provision",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
    }

    [Fact]
    public async Task Create_Device_As_Portal_Sends_It_Should_Return_Created()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspaceId = await CreateWorkspaceAsync();

        var body = $$"""
            {
              "workspaceId": "{{workspaceId}}",
              "name": "Portal Device",
              "identifier": "portal-device-{{Guid.NewGuid():N}}",
              "category": "Sensor"
            }
            """;

        var response = await _client.PostAsync(
            "/api/devices",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
    }

    private async Task<Guid> CreateWorkspaceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = $"Contract-{Guid.NewGuid():N}"
        });

        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!.Id;
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"contract-{Guid.NewGuid():N}@fluxo.local";
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
