using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Application.UseCases.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController : ControllerBase
{
    private readonly CreateWorkspaceUseCase _createWorkspaceUseCase;
    private readonly ListUserWorkspacesUseCase _listUserWorkspacesUseCase;

    public WorkspacesController(
        CreateWorkspaceUseCase createWorkspaceUseCase,
        ListUserWorkspacesUseCase listUserWorkspacesUseCase)
    {
        _createWorkspaceUseCase = createWorkspaceUseCase;
        _listUserWorkspacesUseCase = listUserWorkspacesUseCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _createWorkspaceUseCase.ExecuteAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _listUserWorkspacesUseCase.ExecuteAsync(userId, cancellationToken);
        return Ok(result);
    }
}
