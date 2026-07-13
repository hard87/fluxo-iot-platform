using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Portal;

public sealed class GetAuthorizedDevicesUseCase(IDeviceRepository devices)
{
    public async Task<IReadOnlyList<Device>> ExecuteAsync(Guid workspaceId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var all = await devices.GetAllByWorkspaceAsync(workspaceId, ct);
        var map = all.ToDictionary(x => x.Id);
        if (ids.Distinct().Count() != ids.Count || ids.Any(id => !map.ContainsKey(id)))
            throw new NotFoundException("One or more devices were not found in this workspace.");
        return ids.Select(id => map[id]).ToArray();
    }
}
