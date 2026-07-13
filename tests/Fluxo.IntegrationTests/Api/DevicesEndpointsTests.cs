using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.IntegrationTests.Api;

public class DevicesEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FluxoWebApplicationFactory _factory;

    public DevicesEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_Devices_Should_Return_Created()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();

        var request = new CreateDeviceRequest
        {
            WorkspaceId = workspace.Id,
            Name = "Boiler Pump",
            Identifier = "boiler-pump-01",
            Category = DeviceCategory.Actuator,
            MetadataJson = "{\"line\":\"A\"}"
        };

        var response = await _client.PostAsJsonAsync("/api/devices", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<DeviceResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.WorkspaceId, payload.WorkspaceId);
        Assert.Equal(request.Identifier, payload.Identifier);
    }

    [Fact]
    public async Task Post_Devices_With_Duplicate_Workspace_And_Identifier_Should_Return_Conflict()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync();

        var request = new CreateDeviceRequest
        {
            WorkspaceId = workspace.Id,
            Name = "Pump 1",
            Identifier = "pump-01",
            Category = DeviceCategory.Actuator
        };

        var first = await _client.PostAsJsonAsync("/api/devices", request);
        var second = await _client.PostAsJsonAsync("/api/devices", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Post_Devices_As_Viewer_Should_Return_Forbidden()
    {
        var ownerToken = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var workspace = await CreateWorkspaceAsync();

        var (viewerUserId, viewerToken) = await RegisterAndLoginWithUserIdAsync();
        await AddMembershipAsync(workspace.Id, viewerUserId, WorkspaceMembershipRole.Viewer);

        using var viewerClient = _factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        var request = new CreateDeviceRequest
        {
            WorkspaceId = workspace.Id,
            Name = "Viewer Attempt",
            Identifier = "viewer-attempt-01",
            Category = DeviceCategory.Sensor
        };

        var response = await viewerClient.PostAsJsonAsync("/api/devices", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AddMembershipAsync(Guid workspaceId, Guid userId, WorkspaceMembershipRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        dbContext.WorkspaceMemberships.Add(new WorkspaceMembership(workspaceId, userId, role));
        await dbContext.SaveChangesAsync();
    }

    private async Task<(Guid UserId, string Token)> RegisterAndLoginWithUserIdAsync()
    {
        var email = $"devices-user-{Guid.NewGuid():N}@fluxo.local";
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
        return (payload!.User.UserId, payload.AccessToken);
    }

    private async Task<WorkspaceResponse> CreateWorkspaceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest
        {
            Name = $"Devices Workspace {Guid.NewGuid():N}"
        });

        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        return workspace!;
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"devices-user-{Guid.NewGuid():N}@fluxo.local";
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
