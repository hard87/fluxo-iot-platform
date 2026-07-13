using System.ComponentModel.DataAnnotations;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Portal;

public sealed class WorkspaceDeviceUpsertRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(120)]
    public string Identifier { get; set; } = string.Empty;

    [EnumDataType(typeof(DeviceCategory))]
    public DeviceCategory Category { get; set; }

    public string? MetadataJson { get; set; }
}
