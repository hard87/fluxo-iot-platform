using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Fluxo.Application.UseCases.Devices;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Portal;

public sealed class ListWorkspaceDevicesUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly IDeviceRepository _deviceRepository;
    private readonly TimeSpan _offlineAfter;

    public ListWorkspaceDevicesUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        IDeviceRepository deviceRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _deviceRepository = deviceRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<IReadOnlyList<DeviceResponse>> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            cancellationToken);

        var devices = await _deviceRepository.GetAllByTenantWorkspaceAsync(
            workspace.TenantId,
            workspace.Id,
            cancellationToken);

        return devices
            .Select(device => CreateDeviceUseCase.Map(device, _offlineAfter))
            .ToList();
    }
}
