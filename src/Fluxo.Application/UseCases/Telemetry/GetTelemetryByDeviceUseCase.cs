using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Telemetry;

public class GetTelemetryByDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly ITelemetryIngestionRepository _telemetryIngestionRepository;

    public GetTelemetryByDeviceUseCase(
        IDeviceRepository deviceRepository,
        ITelemetryRepository telemetryRepository,
        ITelemetryIngestionRepository telemetryIngestionRepository)
    {
        _deviceRepository = deviceRepository;
        _telemetryRepository = telemetryRepository;
        _telemetryIngestionRepository = telemetryIngestionRepository;
    }

    public async Task<IReadOnlyList<TelemetryResponse>> ExecuteAsync(
        Guid deviceId,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ValidationException("Device id is required.");

        if (page <= 0)
            throw new ValidationException("Page must be greater than zero.");

        if (pageSize <= 0 || pageSize > 500)
            throw new ValidationException("PageSize must be between 1 and 500.");

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        // Transitional strategy:
        // - New ingestion path (MQTT) is canonical in telemetry_ingestion_records.
        // - Legacy HTTP path is kept in telemetry_records for backward compatibility.
        var ingestionRecords = await _telemetryIngestionRepository.GetByWorkspaceAndDeviceAsync(
            device.WorkspaceId,
            device.Identifier,
            page,
            pageSize,
            cancellationToken);

        if (ingestionRecords.Count > 0)
        {
            return ingestionRecords
                .Select(x => new TelemetryResponse
                {
                    Id = x.Id,
                    DeviceId = device.Id,
                    PayloadJson = x.PayloadJson,
                    OccurredAtUtc = x.OccurredAtUtc,
                    IngestedAtUtc = x.ReceivedAtUtc
                })
                .ToList();
        }

        var records = await _telemetryRepository.GetByDeviceIdAsync(deviceId, page, pageSize, cancellationToken);
        return records.Select(RegisterTelemetryUseCase.Map).ToList();
    }
}
