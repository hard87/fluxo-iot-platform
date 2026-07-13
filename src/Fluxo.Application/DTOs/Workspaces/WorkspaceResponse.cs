using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Workspaces;

public sealed class WorkspaceResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WorkspaceMembershipRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
