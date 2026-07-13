using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.UseCases.Provisioning;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Portal;

public sealed class ProvisionWorkspaceDeviceUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly ProvisionDeviceUseCase _provisionDeviceUseCase;

    public ProvisionWorkspaceDeviceUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        ProvisionDeviceUseCase provisionDeviceUseCase)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _provisionDeviceUseCase = provisionDeviceUseCase;
    }

    public async Task<ProvisionedDeviceResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        WorkspaceDeviceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            WorkspaceMembershipRole.Admin,
            cancellationToken);

        var provisionRequest = new ProvisionDeviceRequest
        {
            TenantId = workspace.TenantId,
            WorkspaceId = workspace.Id,
            Name = request.Name,
            Identifier = request.Identifier,
            Category = request.Category,
            MetadataJson = request.MetadataJson
        };

        return await _provisionDeviceUseCase.ExecuteAsync(provisionRequest, cancellationToken);
    }
}
