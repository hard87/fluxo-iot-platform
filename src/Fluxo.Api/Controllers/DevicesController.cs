using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.UseCases.Devices;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Route("api")]
public class DevicesController : ControllerBase
{
    private readonly CreateDeviceUseCase _createDeviceUseCase;
    private readonly GetDeviceByIdUseCase _getDeviceByIdUseCase;
    private readonly ListDevicesUseCase _listDevicesUseCase;

    public DevicesController(
        CreateDeviceUseCase createDeviceUseCase,
        GetDeviceByIdUseCase getDeviceByIdUseCase,
        ListDevicesUseCase listDevicesUseCase)
    {
        _createDeviceUseCase = createDeviceUseCase;
        _getDeviceByIdUseCase = getDeviceByIdUseCase;
        _listDevicesUseCase = listDevicesUseCase;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createDeviceUseCase.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("devices/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getDeviceByIdUseCase.ExecuteAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workspaces/{workspaceId:guid}/devices")]
    public async Task<IActionResult> GetByWorkspace(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _listDevicesUseCase.ExecuteAsync(workspaceId, cancellationToken);
        return Ok(result);
    }
}
