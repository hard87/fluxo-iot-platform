using System.Net;
using System.Net.Http.Json;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Domain.Enums;

namespace Fluxo.IntegrationTests.Api;

public class DevicesEndpointsTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DevicesEndpointsTests(FluxoWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_Devices_Should_Return_Created()
    {
        var request = new CreateDeviceRequest
        {
            WorkspaceId = Guid.NewGuid(),
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
        var workspaceId = Guid.NewGuid();

        var request = new CreateDeviceRequest
        {
            WorkspaceId = workspaceId,
            Name = "Pump 1",
            Identifier = "pump-01",
            Category = DeviceCategory.Actuator
        };

        var first = await _client.PostAsJsonAsync("/api/devices", request);
        var second = await _client.PostAsJsonAsync("/api/devices", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
