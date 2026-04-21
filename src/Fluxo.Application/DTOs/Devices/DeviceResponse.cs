using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Devices;

public class DeviceResponse
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public DeviceCategory Category { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
