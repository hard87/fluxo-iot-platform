using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Application.UseCases.Telemetry;
using Fluxo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TelemetryController : ControllerBase
{
    private readonly RegisterTelemetryUseCase _registerTelemetryUseCase;
    private readonly GetTelemetryByDeviceUseCase _getTelemetryByDeviceUseCase;
    private readonly GetAuthorizedDeviceUseCase _getAuthorizedDeviceUseCase;

    public TelemetryController(
        RegisterTelemetryUseCase registerTelemetryUseCase,
        GetTelemetryByDeviceUseCase getTelemetryByDeviceUseCase,
        GetAuthorizedDeviceUseCase getAuthorizedDeviceUseCase)
    {
        _registerTelemetryUseCase = registerTelemetryUseCase;
        _getTelemetryByDeviceUseCase = getTelemetryByDeviceUseCase;
        _getAuthorizedDeviceUseCase = getAuthorizedDeviceUseCase;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _getAuthorizedDeviceUseCase.ExecuteAsync(userId, request.DeviceId, WorkspaceMembershipRole.Admin, cancellationToken);

        var result = await _registerTelemetryUseCase.ExecuteAsync(request, cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpGet("devices/{deviceId:guid}/telemetry")]
    public async Task<IActionResult> GetByDeviceId(
        Guid deviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetRequiredUserId();
        await _getAuthorizedDeviceUseCase.ExecuteAsync(userId, deviceId, cancellationToken);

        var result = await _getTelemetryByDeviceUseCase.ExecuteAsync(
            deviceId,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }
}
