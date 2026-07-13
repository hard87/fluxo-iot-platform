using System.ComponentModel.DataAnnotations;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Devices;

public class CreateDeviceRequest
{
    public string? TenantId { get; set; }

    [Required]
    public Guid WorkspaceId { get; set; }

    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Identifier { get; set; } = string.Empty;

    [EnumDataType(typeof(DeviceCategory))]
    public DeviceCategory Category { get; set; }

    public string? MetadataJson { get; set; }
}
