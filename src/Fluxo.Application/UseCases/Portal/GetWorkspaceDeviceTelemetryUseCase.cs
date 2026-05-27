using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Telemetry;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetWorkspaceDeviceTelemetryUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly IDeviceRepository _deviceRepository;
    private readonly GetTelemetryByDeviceUseCase _getTelemetryByDeviceUseCase;

    public GetWorkspaceDeviceTelemetryUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        IDeviceRepository deviceRepository,
        GetTelemetryByDeviceUseCase getTelemetryByDeviceUseCase)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _deviceRepository = deviceRepository;
        _getTelemetryByDeviceUseCase = getTelemetryByDeviceUseCase;
    }

    public async Task<IReadOnlyList<TelemetryResponse>> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        Guid deviceId,
        int page = 1,
        int pageSize = 100,
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

        return await _getTelemetryByDeviceUseCase.ExecuteAsync(deviceId, page, pageSize, cancellationToken);
    }
}
