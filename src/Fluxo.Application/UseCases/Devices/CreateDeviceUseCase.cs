using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Devices;

public class CreateDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;

    public CreateDeviceUseCase(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<DeviceResponse> ExecuteAsync(
        CreateDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkspaceId == Guid.Empty)
            throw new ValidationException("WorkspaceId is required.");

        var existingDevice = await _deviceRepository.GetByWorkspaceAndIdentifierAsync(
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
            request.MetadataJson);

        await _deviceRepository.AddAsync(device, cancellationToken);

        return Map(device);
    }

    internal static DeviceResponse Map(Device device)
    {
        return new DeviceResponse
        {
            Id = device.Id,
            WorkspaceId = device.WorkspaceId,
            Name = device.Name,
            Identifier = device.Identifier,
            Category = device.Category,
            MetadataJson = device.MetadataJson,
            IsActive = device.IsActive,
            CreatedAtUtc = device.CreatedAtUtc
        };
    }
}
