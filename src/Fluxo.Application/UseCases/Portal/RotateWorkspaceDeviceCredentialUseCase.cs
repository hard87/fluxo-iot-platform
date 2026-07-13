using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Provisioning;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Portal;

public sealed class RotateWorkspaceDeviceCredentialUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly IDeviceRepository _deviceRepository;
    private readonly RotateDeviceCredentialUseCase _rotateDeviceCredentialUseCase;

    public RotateWorkspaceDeviceCredentialUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        IDeviceRepository deviceRepository,
        RotateDeviceCredentialUseCase rotateDeviceCredentialUseCase)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _deviceRepository = deviceRepository;
        _rotateDeviceCredentialUseCase = rotateDeviceCredentialUseCase;
    }

    public async Task<RotateDeviceCredentialResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            WorkspaceMembershipRole.Admin,
            cancellationToken);

        var device = await _deviceRepository.GetByTenantWorkspaceAndIdAsync(
            workspace.TenantId,
            workspace.Id,
            deviceId,
            cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        return await _rotateDeviceCredentialUseCase.ExecuteAsync(deviceId, cancellationToken);
    }
}
