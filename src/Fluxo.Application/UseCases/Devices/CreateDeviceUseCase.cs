using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Fluxo.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Devices;

public class CreateDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly TimeSpan _offlineAfter;

    public CreateDeviceUseCase(
        IDeviceRepository deviceRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _deviceRepository = deviceRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<DeviceResponse> ExecuteAsync(
        CreateDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkspaceId == Guid.Empty)
            throw new ValidationException("WorkspaceId is required.");

        var normalizedTenantId = Device.NormalizeTenantId(request.TenantId);

        var existingDevice = await _deviceRepository.GetByTenantWorkspaceAndIdentifierAsync(
            normalizedTenantId,
            request.WorkspaceId,
            request.Identifier,
            cancellationToken);

        if (existingDevice is not null)
            throw new ConflictException("A device with this identifier already exists in this workspace.");

        var device = new Device(
            request.WorkspaceId,
            request.Name,
            request.Identifier,
            request.Category,
            request.MetadataJson,
            normalizedTenantId);

        await _deviceRepository.AddAsync(device, cancellationToken);

        return Map(device, _offlineAfter);
    }

    internal static DeviceResponse Map(Device device, TimeSpan offlineAfter)
    {
        return new DeviceResponse
        {
            Id = device.Id,
            TenantId = device.TenantId,
            WorkspaceId = device.WorkspaceId,
            Name = device.Name,
            Identifier = device.Identifier,
            Category = device.Category,
            MetadataJson = device.MetadataJson,
            IsActive = device.IsActive,
            CreatedAtUtc = device.CreatedAtUtc,
            LastContactAtUtc = device.LastContactAtUtc,
            LastTelemetryReceivedAtUtc = device.LastTelemetryReceivedAtUtc,
            LastTelemetryOccurredAtUtc = device.LastTelemetryOccurredAtUtc,
            LastTelemetrySequence = device.LastTelemetrySequence,
            LastTelemetryPayloadJson = device.LastTelemetryPayloadJson,
            OperationalStatus = device.GetOperationalStatus(DateTime.UtcNow, offlineAfter)
        };
    }
}
