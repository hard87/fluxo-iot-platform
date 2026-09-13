using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.Interfaces.Repositories;

public sealed record WorkspaceMemberSummary(Guid UserId, string Email, WorkspaceMembershipRole Role);

public interface IWorkspaceMembershipRepository
{
    Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default);
    Task<bool> IsUserMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
    Task<WorkspaceMembership?> GetMembershipAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceMembership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceMemberSummary>> ListActiveByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
