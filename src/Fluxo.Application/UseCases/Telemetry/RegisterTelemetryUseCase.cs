using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Telemetry;

public class RegisterTelemetryUseCase
{
    // Legacy HTTP telemetry path kept for backward compatibility while MQTT ingestion
    // becomes the canonical persistence path (telemetry_ingestion_records).
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITelemetryRepository _telemetryRepository;

    public RegisterTelemetryUseCase(
        IDeviceRepository deviceRepository,
        ITelemetryRepository telemetryRepository)
    {
        _deviceRepository = deviceRepository;
        _telemetryRepository = telemetryRepository;
    }

    public async Task<TelemetryResponse> ExecuteAsync(
        CreateTelemetryRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetTrackedByIdAsync(request.DeviceId, cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        if (!device.IsActive)
            throw new ValidationException("Telemetry cannot be registered for inactive devices.");

        var telemetry = new TelemetryRecord(
            request.DeviceId,
            request.PayloadJson,
            request.OccurredAtUtc);

        await _telemetryRepository.AddAsync(telemetry, cancellationToken);

        device.RegisterTelemetrySnapshot(
            telemetry.PayloadJson,
            telemetry.IngestedAtUtc,
            telemetry.OccurredAtUtc,
            sequence: null);

        await _deviceRepository.UpdateAsync(device, cancellationToken);

        return Map(telemetry);
    }

    internal static TelemetryResponse Map(TelemetryRecord telemetry)
    {
        return new TelemetryResponse
        {
            Id = telemetry.Id,
            DeviceId = telemetry.DeviceId,
            PayloadJson = telemetry.PayloadJson,
            OccurredAtUtc = telemetry.OccurredAtUtc,
            IngestedAtUtc = telemetry.IngestedAtUtc
        };
    }
}
