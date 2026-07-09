using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetAuthorizedWorkspaceUseCase
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;

    public GetAuthorizedWorkspaceUseCase(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
    }

    public async Task<Workspace> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(userId, workspaceId, WorkspaceMembershipRole.Viewer, cancellationToken);
    }

    public async Task<Workspace> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        WorkspaceMembershipRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        if (workspaceId == Guid.Empty)
            throw new ValidationException("Workspace id is required.");

        var membership = await _workspaceMembershipRepository.GetMembershipAsync(
            workspaceId,
            userId,
            cancellationToken);

        if (membership is null)
            throw new NotFoundException("Workspace not found.");

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
            throw new NotFoundException("Workspace not found.");

        if (!membership.Role.SatisfiesMinimum(minimumRole))
            throw new ForbiddenException(
                $"Role '{membership.Role}' does not meet the minimum required role '{minimumRole}' for this workspace.");

        return workspace;
    }
}
