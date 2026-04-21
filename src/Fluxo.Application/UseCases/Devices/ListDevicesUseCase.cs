using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Devices;

public class ListDevicesUseCase
{
    private readonly IDeviceRepository _deviceRepository;

    public ListDevicesUseCase(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<IReadOnlyList<DeviceResponse>> ExecuteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
            throw new ValidationException("WorkspaceId is required.");

        var devices = await _deviceRepository.GetAllByWorkspaceAsync(workspaceId, cancellationToken);
        return devices.Select(CreateDeviceUseCase.Map).ToList();
    }
}
