using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public sealed class PlatformUserRepository : IPlatformUserRepository
{
    private readonly FluxoDbContext _context;

    public PlatformUserRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PlatformUser user, CancellationToken cancellationToken = default)
    {
        await _context.PlatformUsers.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlatformUser?> GetByEmailNormalizedAsync(
        string emailNormalized,
        CancellationToken cancellationToken = default)
    {
        var normalized = emailNormalized.Trim().ToUpperInvariant();

        return await _context.PlatformUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmailNormalized == normalized, cancellationToken);
    }

    public async Task<PlatformUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PlatformUser?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformUsers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(PlatformUser user, CancellationToken cancellationToken = default)
    {
        _context.PlatformUsers.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
