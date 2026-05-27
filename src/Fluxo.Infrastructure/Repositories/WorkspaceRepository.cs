using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public sealed class WorkspaceRepository : IWorkspaceRepository
{
    private readonly FluxoDbContext _context;

    public WorkspaceRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        await _context.Workspaces.AddAsync(workspace, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Workspace?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = tenantId.Trim().ToLowerInvariant();

        return await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return [];

        return await _context.Workspaces
            .AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }
}
