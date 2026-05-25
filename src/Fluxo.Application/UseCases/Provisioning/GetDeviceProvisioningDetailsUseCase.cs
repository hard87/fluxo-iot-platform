using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Provisioning;

public sealed class GetDeviceProvisioningDetailsUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCredentialRepository _credentialRepository;
    private readonly TimeSpan _offlineAfter;

    public GetDeviceProvisioningDetailsUseCase(
        IDeviceRepository deviceRepository,
        IDeviceCredentialRepository credentialRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _deviceRepository = deviceRepository;
        _credentialRepository = credentialRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<DeviceProvisioningDetailsResponse> ExecuteAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ValidationException("Device id is required.");

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
            throw new NotFoundException("Device not found.");

        var credential = await _credentialRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);

        return new DeviceProvisioningDetailsResponse
        {
            DeviceId = device.Id,
            TenantId = device.TenantId,
            WorkspaceId = device.WorkspaceId,
            DeviceName = device.Name,
            DeviceIdentifier = device.Identifier,
            DeviceCategory = device.Category,
            DeviceIsActive = device.IsActive,
            DeviceCreatedAtUtc = device.CreatedAtUtc,
            LastContactAtUtc = device.LastContactAtUtc,
            LastTelemetryReceivedAtUtc = device.LastTelemetryReceivedAtUtc,
            LastTelemetryOccurredAtUtc = device.LastTelemetryOccurredAtUtc,
            LastTelemetrySequence = device.LastTelemetrySequence,
            LastTelemetryPayloadJson = device.LastTelemetryPayloadJson,
            OperationalStatus = device.GetOperationalStatus(DateTime.UtcNow, _offlineAfter),
            ActiveCredentialId = credential?.Id,
            ActiveCredentialUsername = credential?.Username,
            ActiveCredentialStatus = credential?.Status,
            ActiveCredentialCreatedAtUtc = credential?.CreatedAtUtc,
            MqttPublishTopic = DeviceProvisioningConventions.BuildMqttPublishTopic(
                device.TenantId,
                device.WorkspaceId,
                device.Identifier)
        };
    }
}
