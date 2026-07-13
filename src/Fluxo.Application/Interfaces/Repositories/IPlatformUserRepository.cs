using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface IPlatformUserRepository
{
    Task AddAsync(PlatformUser user, CancellationToken cancellationToken = default);
    Task<PlatformUser?> GetByEmailNormalizedAsync(string emailNormalized, CancellationToken cancellationToken = default);
    Task<PlatformUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlatformUser?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlatformUser user, CancellationToken cancellationToken = default);
}
