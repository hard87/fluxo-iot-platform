using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Domain.Enums;

namespace Fluxo.IntegrationTests.Api;

public class PortalAuthWorkspaceEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PortalAuthWorkspaceEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_Should_Return_AccessToken()
    {
        var email = $"portal-user-{Guid.NewGuid():N}@fluxo.local";
        var password = "Abcdef!23456";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal(email, payload.User.Email);
    }

    [Fact]
    public async Task Create_Workspace_Should_Associate_User_As_Owner()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = "Officina Planta A"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var workspace = await create.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(workspace);
        Assert.Equal(WorkspaceMembershipRole.Owner, workspace.Role);
        Assert.False(string.IsNullOrWhiteSpace(workspace.TenantId));

        var list = await _client.GetAsync("/api/workspaces");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var workspaces = await list.Content.ReadFromJsonAsync<List<WorkspaceResponse>>();
        Assert.NotNull(workspaces);
        Assert.Contains(workspaces, x => x.Id == workspace.Id);
    }

    [Fact]
    public async Task Provision_And_Rotate_Credential_Should_Work_Inside_User_Workspace()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var workspace = await CreateWorkspaceAsync("Planta B");
        var provision = await _client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/devices/provision",
            new WorkspaceDeviceUpsertRequest
            {
                Name = "ESP32 Linha 1",
                Identifier = $"esp32-{Guid.NewGuid():N}",
                Category = DeviceCategory.Sensor
            });

        Assert.Equal(HttpStatusCode.Created, provision.StatusCode);
        var provisioned = await provision.Content.ReadFromJsonAsync<ProvisionedDeviceResponse>();
        Assert.NotNull(provisioned);
        Assert.False(string.IsNullOrWhiteSpace(provisioned.ProvisioningSecret));

        var rotate = await _client.PostAsync(
            $"/api/workspaces/{workspace.Id}/devices/{provisioned.DeviceId}/credentials/rotate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var rotated = await rotate.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(provisioned.CredentialId, rotated.CredentialId);
        Assert.False(string.IsNullOrWhiteSpace(rotated.ProvisioningSecret));
    }

    [Fact]
    public async Task Should_Not_Access_Device_From_Another_Workspace()
    {
        var tokenA = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var workspaceA = await CreateWorkspaceAsync("Tenant A");

        var provision = await _client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceA.Id}/devices/provision",
            new WorkspaceDeviceUpsertRequest
            {
                Name = "Device A",
                Identifier = $"device-a-{Guid.NewGuid():N}",
                Category = DeviceCategory.Sensor
            });

        provision.EnsureSuccessStatusCode();
        var provisionedA = await provision.Content.ReadFromJsonAsync<ProvisionedDeviceResponse>();
        Assert.NotNull(provisionedA);

        var tokenB = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var workspaceB = await CreateWorkspaceAsync("Tenant B");
        Assert.NotEqual(workspaceA.Id, workspaceB.Id);

        var forbiddenAccess = await _client.GetAsync(
            $"/api/workspaces/{workspaceA.Id}/devices/{provisionedA.DeviceId}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenAccess.StatusCode);
    }

    [Fact]
    public async Task Register_With_Weak_Password_Should_Return_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Email = $"weak-{Guid.NewGuid():N}@fluxo.local",
            Password = "abc"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<WorkspaceResponse> CreateWorkspaceAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = $"{name}-{Guid.NewGuid():N}"
        });

        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!;
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@fluxo.local";
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
