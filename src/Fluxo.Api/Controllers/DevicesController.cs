using Fluxo.Api.Extensions;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.UseCases.Devices;
using Fluxo.Application.UseCases.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class DevicesController : ControllerBase
{
    private readonly CreateDeviceUseCase _createDeviceUseCase;
    private readonly GetDeviceByIdUseCase _getDeviceByIdUseCase;
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly GetAuthorizedDeviceUseCase _getAuthorizedDeviceUseCase;

    public DevicesController(
        CreateDeviceUseCase createDeviceUseCase,
        GetDeviceByIdUseCase getDeviceByIdUseCase,
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        GetAuthorizedDeviceUseCase getAuthorizedDeviceUseCase)
    {
        _createDeviceUseCase = createDeviceUseCase;
        _getDeviceByIdUseCase = getDeviceByIdUseCase;
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _getAuthorizedDeviceUseCase = getAuthorizedDeviceUseCase;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDeviceRequest request,
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

        var result = await _createDeviceUseCase.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("devices/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _getAuthorizedDeviceUseCase.ExecuteAsync(userId, id, cancellationToken);

        var result = await _getDeviceByIdUseCase.ExecuteAsync(id, cancellationToken);
        return Ok(result);
    }
}
