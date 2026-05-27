using System.ComponentModel.DataAnnotations;

namespace Fluxo.Application.DTOs.Workspaces;

public sealed class CreateWorkspaceRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MinLength(3)]
    [MaxLength(120)]
    public string? TenantId { get; set; }
}
