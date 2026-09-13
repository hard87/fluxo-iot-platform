using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Infrastructure.Repositories;

public sealed class WorkspaceMembershipRepository : IWorkspaceMembershipRepository
{
    private readonly FluxoDbContext _context;

    public WorkspaceMembershipRepository(FluxoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default)
    {
        await _context.WorkspaceMemberships.AddAsync(membership, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUserMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkspaceMemberships
            .AsNoTracking()
            .AnyAsync(
                x => x.WorkspaceId == workspaceId && x.UserId == userId,
                cancellationToken);
    }

    public async Task<WorkspaceMembership?> GetMembershipAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkspaceMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == workspaceId && x.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMembership>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkspaceMemberships
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMemberSummary>> ListActiveByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from m in _context.WorkspaceMemberships.AsNoTracking()
            join u in _context.PlatformUsers.AsNoTracking() on m.UserId equals u.Id
            where m.WorkspaceId == workspaceId && u.IsActive
            orderby u.Email
            select new WorkspaceMemberSummary(u.Id, u.Email, m.Role)
        ).ToListAsync(cancellationToken);
    }
}
