using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Fluxo.Application.UseCases.Devices;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetWorkspaceDeviceUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly IDeviceRepository _deviceRepository;
    private readonly TimeSpan _offlineAfter;

    public GetWorkspaceDeviceUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        IDeviceRepository deviceRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _deviceRepository = deviceRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<DeviceResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            cancellationToken);

        var device = await _deviceRepository.GetByTenantWorkspaceAndIdAsync(
            workspace.TenantId,
            workspace.Id,
            deviceId,
            cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        return CreateDeviceUseCase.Map(device, _offlineAfter);
    }
}
