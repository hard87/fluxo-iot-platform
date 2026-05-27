using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Workspaces;

public sealed class ListUserWorkspacesUseCase
{
    private readonly IPlatformUserRepository _userRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;

    public ListUserWorkspacesUseCase(
        IPlatformUserRepository userRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository)
    {
        _userRepository = userRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
    }

    public async Task<IReadOnlyList<WorkspaceResponse>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("User is not authenticated.");

        var memberships = await _workspaceMembershipRepository.GetByUserIdAsync(userId, cancellationToken);
        var workspaceIds = memberships.Select(x => x.WorkspaceId).Distinct().ToList();
        var workspaces = await _workspaceRepository.GetByIdsAsync(workspaceIds, cancellationToken);
        var workspacesById = workspaces.ToDictionary(x => x.Id, x => x);

        return memberships
            .Where(x => workspacesById.ContainsKey(x.WorkspaceId))
            .Select(x =>
            {
                var workspace = workspacesById[x.WorkspaceId];
                return new WorkspaceResponse
                {
                    Id = workspace.Id,
                    TenantId = workspace.TenantId,
                    Name = workspace.Name,
                    Role = x.Role,
                    CreatedAtUtc = workspace.CreatedAtUtc
                };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }
}
