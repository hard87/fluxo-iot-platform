using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.UseCases.Devices;

namespace Fluxo.Application.UseCases.Portal;

public sealed class CreateWorkspaceDeviceUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly CreateDeviceUseCase _createDeviceUseCase;

    public CreateWorkspaceDeviceUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        CreateDeviceUseCase createDeviceUseCase)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _createDeviceUseCase = createDeviceUseCase;
    }

    public async Task<DeviceResponse> ExecuteAsync(
        Guid userId,
        Guid workspaceId,
        WorkspaceDeviceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _getAuthorizedWorkspaceUseCase.ExecuteAsync(
            userId,
            workspaceId,
            cancellationToken);

        var createRequest = new CreateDeviceRequest
        {
            TenantId = workspace.TenantId,
            WorkspaceId = workspace.Id,
            Name = request.Name,
            Identifier = request.Identifier,
            Category = request.Category,
            MetadataJson = request.MetadataJson
        };

        return await _createDeviceUseCase.ExecuteAsync(createRequest, cancellationToken);
    }
}
