using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Provisioning;

public sealed class ProvisionedDeviceResponse
{
    public Guid DeviceId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public DeviceCategory DeviceCategory { get; set; }
    public bool DeviceIsActive { get; set; }
    public DateTime DeviceCreatedAtUtc { get; set; }
    public DateTime? LastContactAtUtc { get; set; }
    public DeviceOperationalStatus OperationalStatus { get; set; }
    public Guid CredentialId { get; set; }
    public string CredentialUsername { get; set; } = string.Empty;
    public DeviceCredentialStatus CredentialStatus { get; set; }
    public DateTime CredentialCreatedAtUtc { get; set; }
    public string ProvisioningSecret { get; set; } = string.Empty;
    public string MqttPublishTopic { get; set; } = string.Empty;
}
