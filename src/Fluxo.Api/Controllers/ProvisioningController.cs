using Fluxo.Api.Extensions;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Application.UseCases.Provisioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/provisioning")]
public sealed class ProvisioningController : ControllerBase
{
    private readonly ProvisionDeviceUseCase _provisionDeviceUseCase;
    private readonly GetDeviceProvisioningDetailsUseCase _getDeviceProvisioningDetailsUseCase;
    private readonly RotateDeviceCredentialUseCase _rotateDeviceCredentialUseCase;
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly GetAuthorizedDeviceUseCase _getAuthorizedDeviceUseCase;

    public ProvisioningController(
        ProvisionDeviceUseCase provisionDeviceUseCase,
        GetDeviceProvisioningDetailsUseCase getDeviceProvisioningDetailsUseCase,
        RotateDeviceCredentialUseCase rotateDeviceCredentialUseCase,
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        GetAuthorizedDeviceUseCase getAuthorizedDeviceUseCase)
    {
        _provisionDeviceUseCase = provisionDeviceUseCase;
        _getDeviceProvisioningDetailsUseCase = getDeviceProvisioningDetailsUseCase;
        _rotateDeviceCredentialUseCase = rotateDeviceCredentialUseCase;
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _getAuthorizedDeviceUseCase = getAuthorizedDeviceUseCase;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> ProvisionDevice(
        [FromBody] ProvisionDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            request.WorkspaceId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TenantId) &&
            !string.Equals(request.TenantId.Trim(), workspace.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("TenantId does not match the authorized workspace.");
        }

        request.TenantId = workspace.TenantId;

        var result = await _provisionDeviceUseCase.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDeviceProvisioning), new { deviceId = result.DeviceId }, result);
    }

    [HttpGet("devices/{deviceId:guid}")]
    public async Task<IActionResult> GetDeviceProvisioning(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _getAuthorizedDeviceUseCase.ExecuteAsync(userId, deviceId, cancellationToken);

        var result = await _getDeviceProvisioningDetailsUseCase.ExecuteAsync(deviceId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("devices/{deviceId:guid}/credentials/rotate")]
    public async Task<IActionResult> RotateCredential(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _getAuthorizedDeviceUseCase.ExecuteAsync(userId, deviceId, cancellationToken);

        var result = await _rotateDeviceCredentialUseCase.ExecuteAsync(deviceId, cancellationToken);
        return Ok(result);
    }
}
