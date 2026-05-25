using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.UseCases.Provisioning;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Route("api/provisioning")]
public sealed class ProvisioningController : ControllerBase
{
    private readonly ProvisionDeviceUseCase _provisionDeviceUseCase;
    private readonly GetDeviceProvisioningDetailsUseCase _getDeviceProvisioningDetailsUseCase;
    private readonly RotateDeviceCredentialUseCase _rotateDeviceCredentialUseCase;

    public ProvisioningController(
        ProvisionDeviceUseCase provisionDeviceUseCase,
        GetDeviceProvisioningDetailsUseCase getDeviceProvisioningDetailsUseCase,
        RotateDeviceCredentialUseCase rotateDeviceCredentialUseCase)
    {
        _provisionDeviceUseCase = provisionDeviceUseCase;
        _getDeviceProvisioningDetailsUseCase = getDeviceProvisioningDetailsUseCase;
        _rotateDeviceCredentialUseCase = rotateDeviceCredentialUseCase;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> ProvisionDevice(
        [FromBody] ProvisionDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _provisionDeviceUseCase.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDeviceProvisioning), new { deviceId = result.DeviceId }, result);
    }

    [HttpGet("devices/{deviceId:guid}")]
    public async Task<IActionResult> GetDeviceProvisioning(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var result = await _getDeviceProvisioningDetailsUseCase.ExecuteAsync(deviceId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("devices/{deviceId:guid}/credentials/rotate")]
    public async Task<IActionResult> RotateCredential(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var result = await _rotateDeviceCredentialUseCase.ExecuteAsync(deviceId, cancellationToken);
        return Ok(result);
    }
}
