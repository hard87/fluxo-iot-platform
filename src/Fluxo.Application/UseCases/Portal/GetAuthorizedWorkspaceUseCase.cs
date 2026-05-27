using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

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
        if (userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        if (workspaceId == Guid.Empty)
            throw new ValidationException("Workspace id is required.");

        var hasAccess = await _workspaceMembershipRepository.IsUserMemberAsync(
            workspaceId,
            userId,
            cancellationToken);

        if (!hasAccess)
            throw new NotFoundException("Workspace not found.");

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
            throw new NotFoundException("Workspace not found.");

        return workspace;
    }
}
