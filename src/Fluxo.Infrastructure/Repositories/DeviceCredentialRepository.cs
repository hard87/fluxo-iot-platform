using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public class DeviceCredentialRepository : IDeviceCredentialRepository
{
    private readonly FluxoDbContext _context;

    public DeviceCredentialRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceCredential credential, CancellationToken cancellationToken = default)
    {
        await _context.DeviceCredentials.AddAsync(credential, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<DeviceCredential?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();

        return await _context.DeviceCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);
    }

    public async Task<DeviceCredential?> GetActiveByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive, cancellationToken);
    }

    public async Task<DeviceCredential?> GetTrackedActiveByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCredentials
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive, cancellationToken);
    }

    public async Task RotateAsync(
        Guid deviceId,
        DeviceCredential newCredential,
        CancellationToken cancellationToken = default)
    {
        if (IsInMemoryProvider())
        {
            var activeCredentialInMemory = await _context.DeviceCredentials
                .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive, cancellationToken);

            activeCredentialInMemory?.Revoke(DateTime.UtcNow);
            await _context.DeviceCredentials.AddAsync(newCredential, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var activeCredential = await _context.DeviceCredentials
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive, cancellationToken);

        activeCredential?.Revoke(DateTime.UtcNow);

        await _context.DeviceCredentials.AddAsync(newCredential, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private bool IsInMemoryProvider()
    {
        return string.Equals(
            _context.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task UpdateAsync(DeviceCredential credential, CancellationToken cancellationToken = default)
    {
        _context.DeviceCredentials.Update(credential);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
