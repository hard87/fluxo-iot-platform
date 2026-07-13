using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.UseCases.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}")]
public sealed class PortalDevicesController : ControllerBase
{
    private readonly CreateWorkspaceDeviceUseCase _createWorkspaceDeviceUseCase;
    private readonly ListWorkspaceDevicesUseCase _listWorkspaceDevicesUseCase;
    private readonly GetWorkspaceDeviceUseCase _getWorkspaceDeviceUseCase;
    private readonly ProvisionWorkspaceDeviceUseCase _provisionWorkspaceDeviceUseCase;
    private readonly GetWorkspaceDeviceProvisioningUseCase _getWorkspaceDeviceProvisioningUseCase;
    private readonly RotateWorkspaceDeviceCredentialUseCase _rotateWorkspaceDeviceCredentialUseCase;
    private readonly GetWorkspaceDeviceTelemetryUseCase _getWorkspaceDeviceTelemetryUseCase;
    private readonly GetWorkspaceDashboardUseCase _getWorkspaceDashboardUseCase;

    public PortalDevicesController(
        CreateWorkspaceDeviceUseCase createWorkspaceDeviceUseCase,
        ListWorkspaceDevicesUseCase listWorkspaceDevicesUseCase,
        GetWorkspaceDeviceUseCase getWorkspaceDeviceUseCase,
        ProvisionWorkspaceDeviceUseCase provisionWorkspaceDeviceUseCase,
        GetWorkspaceDeviceProvisioningUseCase getWorkspaceDeviceProvisioningUseCase,
        RotateWorkspaceDeviceCredentialUseCase rotateWorkspaceDeviceCredentialUseCase,
        GetWorkspaceDeviceTelemetryUseCase getWorkspaceDeviceTelemetryUseCase,
        GetWorkspaceDashboardUseCase getWorkspaceDashboardUseCase)
    {
        _createWorkspaceDeviceUseCase = createWorkspaceDeviceUseCase;
        _listWorkspaceDevicesUseCase = listWorkspaceDevicesUseCase;
        _getWorkspaceDeviceUseCase = getWorkspaceDeviceUseCase;
        _provisionWorkspaceDeviceUseCase = provisionWorkspaceDeviceUseCase;
        _getWorkspaceDeviceProvisioningUseCase = getWorkspaceDeviceProvisioningUseCase;
        _rotateWorkspaceDeviceCredentialUseCase = rotateWorkspaceDeviceCredentialUseCase;
        _getWorkspaceDeviceTelemetryUseCase = getWorkspaceDeviceTelemetryUseCase;
        _getWorkspaceDashboardUseCase = getWorkspaceDashboardUseCase;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _getWorkspaceDashboardUseCase.ExecuteAsync(userId, workspaceId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _listWorkspaceDevicesUseCase.ExecuteAsync(userId, workspaceId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("devices")]
    public async Task<IActionResult> CreateDevice(
        Guid workspaceId,
        [FromBody] WorkspaceDeviceUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _createWorkspaceDeviceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetDevice),
            new { workspaceId, deviceId = result.Id },
            result);
    }

    [HttpPost("devices/provision")]
    public async Task<IActionResult> ProvisionDevice(
        Guid workspaceId,
        [FromBody] WorkspaceDeviceUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _provisionWorkspaceDeviceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetProvisioningDetails),
            new { workspaceId, deviceId = result.DeviceId },
            result);
    }

    [HttpGet("devices/{deviceId:guid}")]
    public async Task<IActionResult> GetDevice(
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _getWorkspaceDeviceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            deviceId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("devices/{deviceId:guid}/provisioning")]
    public async Task<IActionResult> GetProvisioningDetails(
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _getWorkspaceDeviceProvisioningUseCase.ExecuteAsync(
            userId,
            workspaceId,
            deviceId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("devices/{deviceId:guid}/credentials/rotate")]
    public async Task<IActionResult> RotateCredential(
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _rotateWorkspaceDeviceCredentialUseCase.ExecuteAsync(
            userId,
            workspaceId,
            deviceId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("devices/{deviceId:guid}/telemetry")]
    public async Task<IActionResult> GetTelemetry(
        Guid workspaceId,
        Guid deviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _getWorkspaceDeviceTelemetryUseCase.ExecuteAsync(
            userId,
            workspaceId,
            deviceId,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }
}
