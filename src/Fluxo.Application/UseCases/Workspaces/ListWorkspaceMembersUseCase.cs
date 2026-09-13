using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Portal;

namespace Fluxo.Application.UseCases.Workspaces;

public sealed class ListWorkspaceMembersUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _authorize;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;

    public ListWorkspaceMembersUseCase(
        GetAuthorizedWorkspaceUseCase authorize,
        IWorkspaceMembershipRepository workspaceMembershipRepository)
    {
        _authorize = authorize;
        _workspaceMembershipRepository = workspaceMembershipRepository;
    }

    public async Task<IReadOnlyList<WorkspaceMemberSummary>> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authorize.ExecuteAsync(userId, workspaceId, cancellationToken);
        return await _workspaceMembershipRepository.ListActiveByWorkspaceAsync(workspaceId, cancellationToken);
    }
}
