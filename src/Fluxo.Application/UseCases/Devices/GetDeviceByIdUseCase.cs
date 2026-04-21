using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Devices;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Devices;

public class GetDeviceByIdUseCase
{
    private readonly IDeviceRepository _deviceRepository;

    public GetDeviceByIdUseCase(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<DeviceResponse> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ValidationException("Device id is required.");

        var device = await _deviceRepository.GetByIdAsync(id, cancellationToken);

        if (device is null)
            throw new NotFoundException("Device not found.");

        return CreateDeviceUseCase.Map(device);
    }
}
