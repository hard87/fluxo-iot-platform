using System.ComponentModel.DataAnnotations;

namespace Fluxo.Application.DTOs.Workspaces;

public sealed class UpdateWorkspaceRequest
{
    [Required, MinLength(3), MaxLength(120)]
    public string Name { get; set; } = string.Empty;
}
