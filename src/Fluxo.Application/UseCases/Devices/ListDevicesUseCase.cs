using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Devices;

public class ListDevicesUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly TimeSpan _offlineAfter;

    public ListDevicesUseCase(
        IDeviceRepository deviceRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _deviceRepository = deviceRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<IReadOnlyList<DeviceResponse>> ExecuteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
            throw new ValidationException("WorkspaceId is required.");

        var devices = await _deviceRepository.GetAllByWorkspaceAsync(workspaceId, cancellationToken);
        return devices.Select(device => CreateDeviceUseCase.Map(device, _offlineAfter)).ToList();
    }
}
