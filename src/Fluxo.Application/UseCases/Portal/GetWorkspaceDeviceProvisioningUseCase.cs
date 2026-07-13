using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.UseCases.Provisioning;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetWorkspaceDeviceProvisioningUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly GetDeviceProvisioningDetailsUseCase _getDeviceProvisioningDetailsUseCase;

    public GetWorkspaceDeviceProvisioningUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        GetDeviceProvisioningDetailsUseCase getDeviceProvisioningDetailsUseCase)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _getDeviceProvisioningDetailsUseCase = getDeviceProvisioningDetailsUseCase;
    }

    public async Task<DeviceProvisioningDetailsResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            cancellationToken);

        var details = await _getDeviceProvisioningDetailsUseCase.ExecuteAsync(
            deviceId,
            cancellationToken);

        if (details.WorkspaceId != workspace.Id ||
            !string.Equals(details.TenantId, workspace.TenantId, StringComparison.Ordinal))
        {
            throw new NotFoundException("Device not found.");
        }

        return details;
    }
}
