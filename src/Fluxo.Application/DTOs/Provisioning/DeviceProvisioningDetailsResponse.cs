using Fluxo.Domain.Enums;

namespace Fluxo.Application.DTOs.Provisioning;

public sealed class DeviceProvisioningDetailsResponse
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
    public DateTime? LastTelemetryReceivedAtUtc { get; set; }
    public DateTime? LastTelemetryOccurredAtUtc { get; set; }
    public long? LastTelemetrySequence { get; set; }
    public string? LastTelemetryPayloadJson { get; set; }
    public DeviceOperationalStatus OperationalStatus { get; set; }
    public Guid? ActiveCredentialId { get; set; }
    public string? ActiveCredentialUsername { get; set; }
    public DeviceCredentialStatus? ActiveCredentialStatus { get; set; }
    public DateTime? ActiveCredentialCreatedAtUtc { get; set; }
    public string MqttPublishTopic { get; set; } = string.Empty;
}
