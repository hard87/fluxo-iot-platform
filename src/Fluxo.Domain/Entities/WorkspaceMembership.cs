using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership()
    {
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceMembershipRole Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public WorkspaceMembership(
        Guid workspaceId,
        Guid userId,
        WorkspaceMembershipRole role)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId is required.", nameof(workspaceId));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (!Enum.IsDefined(role))
            throw new ArgumentException("Role is invalid.", nameof(role));

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
