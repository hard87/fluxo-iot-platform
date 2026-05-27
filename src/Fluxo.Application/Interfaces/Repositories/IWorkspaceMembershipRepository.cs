using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface IWorkspaceMembershipRepository
{
    Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default);
    Task<bool> IsUserMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceMembership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
