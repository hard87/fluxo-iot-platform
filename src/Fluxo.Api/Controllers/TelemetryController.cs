using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.UseCases.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Route("api")]
public class TelemetryController : ControllerBase
{
    private readonly RegisterTelemetryUseCase _registerTelemetryUseCase;
    private readonly GetTelemetryByDeviceUseCase _getTelemetryByDeviceUseCase;

    public TelemetryController(
        RegisterTelemetryUseCase registerTelemetryUseCase,
        GetTelemetryByDeviceUseCase getTelemetryByDeviceUseCase)
    {
        _registerTelemetryUseCase = registerTelemetryUseCase;
        _getTelemetryByDeviceUseCase = getTelemetryByDeviceUseCase;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTelemetryRequest request,
        CancellationToken cancellationToken)
    {
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
        var result = await _getTelemetryByDeviceUseCase.ExecuteAsync(
            deviceId,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }
}
