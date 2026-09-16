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
    private readonly ListWorkspaceMembersUseCase _listWorkspaceMembersUseCase;
    private readonly UpdateWorkspaceUseCase _updateWorkspaceUseCase;

    public WorkspacesController(
        CreateWorkspaceUseCase createWorkspaceUseCase,
        ListUserWorkspacesUseCase listUserWorkspacesUseCase,
        ListWorkspaceMembersUseCase listWorkspaceMembersUseCase,
        UpdateWorkspaceUseCase updateWorkspaceUseCase)
    {
        _createWorkspaceUseCase = createWorkspaceUseCase;
        _listUserWorkspacesUseCase = listUserWorkspacesUseCase;
        _listWorkspaceMembersUseCase = listWorkspaceMembersUseCase;
        _updateWorkspaceUseCase = updateWorkspaceUseCase;
    }

    [HttpPatch("{workspaceId:guid}")]
    public async Task<IActionResult> Update(
        Guid workspaceId,
        [FromBody] UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateWorkspaceUseCase.ExecuteAsync(
            User.GetRequiredUserId(), workspaceId, request, cancellationToken);
        return Ok(result);
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

    [HttpGet("{workspaceId:guid}/members")]
    public async Task<IActionResult> Members(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _listWorkspaceMembersUseCase.ExecuteAsync(userId, workspaceId, cancellationToken);
        return Ok(result);
    }
}
