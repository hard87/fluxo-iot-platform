using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Devices;

public class GetDeviceByIdUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly TimeSpan _offlineAfter;

    public GetDeviceByIdUseCase(
        IDeviceRepository deviceRepository,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _deviceRepository = deviceRepository;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<DeviceResponse> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ValidationException("Device id is required.");

        var device = await _deviceRepository.GetByIdAsync(id, cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        return CreateDeviceUseCase.Map(device, _offlineAfter);
    }
}
