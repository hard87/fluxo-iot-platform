using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Telemetry;

public class GetTelemetryByDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITelemetryRepository _telemetryRepository;

    public GetTelemetryByDeviceUseCase(
        IDeviceRepository deviceRepository,
        ITelemetryRepository telemetryRepository)
    {
        _deviceRepository = deviceRepository;
        _telemetryRepository = telemetryRepository;
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

        var records = await _telemetryRepository.GetByDeviceIdAsync(deviceId, page, pageSize, cancellationToken);
        return records.Select(RegisterTelemetryUseCase.Map).ToList();
    }
}
