using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetAuthorizedDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;

    public GetAuthorizedDeviceUseCase(
        IDeviceRepository deviceRepository,
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase)
    {
        _deviceRepository = deviceRepository;
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
    }

    public async Task<Device> ExecuteAsync(
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        if (deviceId == Guid.Empty)
            throw new ValidationException("Device id is required.");

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
            throw new NotFoundException("Device not found.");

        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            device.WorkspaceId,
            cancellationToken);

        if (!string.Equals(device.TenantId, workspace.TenantId, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("Device not found.");

        return device;
    }
}
