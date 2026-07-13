using Fluxo.Domain.Entities;
namespace Fluxo.Application.Interfaces.Repositories;
public interface ITelemetryPointWriter
{
    Task WriteAsync(IReadOnlyCollection<TelemetryPoint> points, CancellationToken cancellationToken = default);
}
