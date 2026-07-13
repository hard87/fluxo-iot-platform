using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Fluxo.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetWorkspaceDashboardUseCase
{
    private readonly GetAuthorizedWorkspaceUseCase _getAuthorizedWorkspaceUseCase;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITelemetryIngestionRepository _telemetryIngestionRepository;
    private readonly ITelemetryIngestionRejectionRepository _telemetryIngestionRejectionRepository;
    private readonly TimeSpan _offlineAfter;

    public GetWorkspaceDashboardUseCase(
        GetAuthorizedWorkspaceUseCase getAuthorizedWorkspaceUseCase,
        IDeviceRepository deviceRepository,
        ITelemetryIngestionRepository telemetryIngestionRepository,
        ITelemetryIngestionRejectionRepository telemetryIngestionRejectionRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _getAuthorizedWorkspaceUseCase = getAuthorizedWorkspaceUseCase;
        _deviceRepository = deviceRepository;
        _telemetryIngestionRepository = telemetryIngestionRepository;
        _telemetryIngestionRejectionRepository = telemetryIngestionRejectionRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<WorkspaceDashboardResponse> ExecuteAsync(
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

        var nowUtc = DateTime.UtcNow;
        var online = 0;
        var offline = 0;
        var unknown = 0;

        foreach (var device in devices)
        {
            var status = device.GetOperationalStatus(nowUtc, _offlineAfter);
            switch (status)
            {
                case DeviceOperationalStatus.Online:
                    online++;
                    break;
                case DeviceOperationalStatus.Offline:
                    offline++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        var processed = await _telemetryIngestionRepository.CountByTenantWorkspaceAsync(
            workspace.TenantId,
            workspace.Id,
            cancellationToken);

        var rejected = await _telemetryIngestionRejectionRepository.CountByTenantWorkspaceAsync(
            workspace.TenantId,
            workspace.Id,
            cancellationToken);

        var lastTelemetry = devices
            .Where(x => x.LastTelemetryReceivedAtUtc.HasValue)
            .MaxBy(x => x.LastTelemetryReceivedAtUtc)?
            .LastTelemetryReceivedAtUtc;

        return new WorkspaceDashboardResponse
        {
            WorkspaceId = workspace.Id,
            TenantId = workspace.TenantId,
            DevicesTotal = devices.Count,
            DevicesOnline = online,
            DevicesOffline = offline,
            DevicesUnknown = unknown,
            LastTelemetryReceivedAtUtc = lastTelemetry,
            MessagesProcessed = processed,
            MessagesRejected = rejected
        };
    }
}
