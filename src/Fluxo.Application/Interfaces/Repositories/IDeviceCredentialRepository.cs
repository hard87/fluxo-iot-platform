using Fluxo.Domain.Entities;

namespace Fluxo.Application.Interfaces.Repositories;

public interface IDeviceCredentialRepository
{
    Task AddAsync(DeviceCredential credential, CancellationToken cancellationToken = default);
    Task<DeviceCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeviceCredential?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<DeviceCredential?> GetActiveByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<DeviceCredential?> GetTrackedActiveByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task RotateAsync(Guid deviceId, DeviceCredential newCredential, CancellationToken cancellationToken = default);
    Task UpdateAsync(DeviceCredential credential, CancellationToken cancellationToken = default);
}
