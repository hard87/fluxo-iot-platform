using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workspace?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workspace>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
