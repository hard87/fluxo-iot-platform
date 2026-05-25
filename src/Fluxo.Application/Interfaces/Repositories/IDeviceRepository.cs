using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Device?> GetByWorkspaceAndIdentifierAsync(
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default);
    Task<Device?> GetByTenantWorkspaceAndIdentifierAsync(
        string tenantId,
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default);
    Task<Device?> GetTrackedByTenantWorkspaceAndIdentifierAsync(
        string tenantId,
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetAllByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);
}
