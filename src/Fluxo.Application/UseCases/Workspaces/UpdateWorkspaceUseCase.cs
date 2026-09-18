using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Workspaces;

public sealed class UpdateWorkspaceUseCase(
    GetAuthorizedWorkspaceUseCase authorize,
    IWorkspaceRepository workspaces,
    IWorkspaceMembershipRepository memberships)
{
    public async Task<WorkspaceResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ValidationException("Request is required.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 3 or > 120)
            throw new ValidationException("Workspace name must contain between 3 and 120 characters.");

        var workspace = await authorize.ExecuteAsync(
            userId, workspaceId, WorkspaceMembershipRole.Admin, cancellationToken);
        var membership = await memberships.GetMembershipAsync(workspaceId, userId, cancellationToken)
            ?? throw new UnauthorizedException("User is not authenticated.");

        workspace.Rename(name);
        await workspaces.UpdateAsync(workspace, cancellationToken);

        return new WorkspaceResponse
        {
            Id = workspace.Id,
            TenantId = workspace.TenantId,
            Name = workspace.Name,
            Role = membership.Role,
            CreatedAtUtc = workspace.CreatedAtUtc
        };
    }
}
