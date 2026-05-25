using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Provisioning;

public sealed class RotateDeviceCredentialResponse
{
    public Guid DeviceId { get; set; }
    public Guid CredentialId { get; set; }
    public string CredentialUsername { get; set; } = string.Empty;
    public DeviceCredentialStatus CredentialStatus { get; set; }
    public DateTime CredentialCreatedAtUtc { get; set; }
    public string ProvisioningSecret { get; set; } = string.Empty;
    public string MqttPublishTopic { get; set; } = string.Empty;
}
