using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.IntegrationTests.Api;

public class TelemetryRejectionsEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FluxoWebApplicationFactory _factory;

    public TelemetryRejectionsEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Without_Token_Should_Return_Unauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/workspaces/{Guid.NewGuid()}/telemetry-rejections");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Should_Return_Empty_Page_When_No_Rejections()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync("Sem rejeições");

        var page = await GetPageAsync(workspace.Id);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Get_Should_Page_Rejections_Newest_First_Scoped_To_Workspace()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync("Com volume");
        var baseInstant = DateTime.UtcNow.AddHours(-2);

        for (var i = 0; i < 25; i++)
        {
            await SeedRejectionAsync(
                workspace,
                receivedAtUtc: baseInstant.AddMinutes(i),
                reason: $"linha {i:00}",
                deviceId: $"edge-{i:00}");
        }

        var first = await GetPageAsync(workspace.Id, page: 1, pageSize: 10);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(25, first.TotalCount);
        // baseInstant.AddMinutes(24) is the newest, so it must lead an ascending-by-age listing.
        Assert.Equal("linha 24", first.Items[0].Reason);
        Assert.True(first.Items[0].ReceivedAtUtc >= first.Items[1].ReceivedAtUtc);

        var last = await GetPageAsync(workspace.Id, page: 3, pageSize: 10);
        Assert.Equal(5, last.Items.Count);
        Assert.Equal("linha 00", last.Items[^1].Reason);
    }

    [Fact]
    public async Task Get_Should_Not_Expose_Rejections_From_Another_Workspace()
    {
        var tokenA = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var workspaceA = await CreateWorkspaceAsync("Tenant A");
        await SeedRejectionAsync(workspaceA, DateTime.UtcNow, "somente A", "device-a");

        var tokenB = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var workspaceB = await CreateWorkspaceAsync("Tenant B");
        await SeedRejectionAsync(workspaceB, DateTime.UtcNow, "somente B", "device-b");

        var ownPage = await GetPageAsync(workspaceB.Id);
        Assert.Single(ownPage.Items);
        Assert.Equal("somente B", ownPage.Items[0].Reason);

        // B is not a member of A: the workspace must look absent, not merely empty.
        var crossResponse = await _client.GetAsync(
            $"/api/workspaces/{workspaceA.Id}/telemetry-rejections");
        Assert.Equal(HttpStatusCode.NotFound, crossResponse.StatusCode);
    }

    [Fact]
    public async Task Get_Should_Filter_By_Search_Term()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync("Busca");

        await SeedRejectionAsync(workspace, DateTime.UtcNow.AddMinutes(-2), "schema inválido", "compressor-01");
        await SeedRejectionAsync(workspace, DateTime.UtcNow.AddMinutes(-1), "payload corrompido", "bomba-02");

        var page = await GetPageAsync(workspace.Id, search: "compressor");

        Assert.Single(page.Items);
        Assert.Equal("compressor-01", page.Items[0].DeviceId);
    }

    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=101")]
    public async Task Get_Should_Reject_Out_Of_Range_Paging(string queryString)
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync("Paginação inválida");

        var response = await _client.GetAsync(
            $"/api/workspaces/{workspace.Id}/telemetry-rejections?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Should_Return_Bounded_Single_Line_Payload_Preview()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspace = await CreateWorkspaceAsync("Payload longo");
        var rawPayload = "{\n  \"noise\": \"" + new string('x', 600) + "\"\n}";

        await SeedRejectionAsync(workspace, DateTime.UtcNow, "payload gigante", "edge-x", payloadRaw: rawPayload);

        var page = await GetPageAsync(workspace.Id);
        var preview = Assert.Single(page.Items).PayloadPreview;

        Assert.DoesNotContain('\n', preview);
        Assert.True(preview.Length <= 241, $"preview length was {preview.Length}");
        Assert.EndsWith("…", preview);
    }

    private async Task<TelemetryRejectionPageResponse> GetPageAsync(
        Guid workspaceId, int page = 1, int pageSize = 20, string? search = null)
    {
        var query = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _client.GetAsync(
            $"/api/workspaces/{workspaceId}/telemetry-rejections?{query}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TelemetryRejectionPageResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task SeedRejectionAsync(
        WorkspaceResponse workspace,
        DateTime receivedAtUtc,
        string reason,
        string deviceId,
        string? payloadRaw = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        dbContext.TelemetryIngestionRejectionRecords.Add(new TelemetryIngestionRejectionRecord(
            receivedAtUtc,
            topic: $"fluxo/tenants/{workspace.TenantId}/workspaces/{workspace.Id}/devices/{deviceId}/telemetry",
            payloadRaw: payloadRaw ?? "{\"invalid\":true}",
            errorType: TelemetryIngestionFailureType.Validation,
            reason: reason,
            tenantId: workspace.TenantId,
            workspaceId: workspace.Id,
            deviceId: deviceId,
            messageType: "telemetry",
            sequence: 1));
        await dbContext.SaveChangesAsync();
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
        var email = $"rejections-user-{Guid.NewGuid():N}@fluxo.local";
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
