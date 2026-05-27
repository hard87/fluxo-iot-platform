using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly FluxoDbContext _context;

    public DeviceRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _context.Devices.AddAsync(device, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Device?> GetByWorkspaceAndIdentifierAsync(
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        return await GetByTenantWorkspaceAndIdentifierAsync(
            Device.DefaultTenantId,
            workspaceId,
            identifier,
            cancellationToken);
    }

    public async Task<Device?> GetByTenantWorkspaceAndIdentifierAsync(
        string tenantId,
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);
        var normalizedIdentifier = identifier.Trim();

        return await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == normalizedTenantId &&
                     x.WorkspaceId == workspaceId &&
                     x.Identifier == normalizedIdentifier,
                cancellationToken);
    }

    public async Task<Device?> GetTrackedByTenantWorkspaceAndIdentifierAsync(
        string tenantId,
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);
        var normalizedIdentifier = identifier.Trim();

        return await _context.Devices
            .FirstOrDefaultAsync(
                x => x.TenantId == normalizedTenantId &&
                     x.WorkspaceId == workspaceId &&
                     x.Identifier == normalizedIdentifier,
                cancellationToken);
    }

    public async Task<Device?> GetByTenantWorkspaceAndIdAsync(
        string tenantId,
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);

        return await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == deviceId &&
                     x.TenantId == normalizedTenantId &&
                     x.WorkspaceId == workspaceId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetAllByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetAllByTenantWorkspaceAsync(
        string tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = Device.NormalizeTenantId(tenantId);

        return await _context.Devices
            .AsNoTracking()
            .Where(x => x.TenantId == normalizedTenantId && x.WorkspaceId == workspaceId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        _context.Devices.Update(device);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
