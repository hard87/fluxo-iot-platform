using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Infrastructure.Data;
namespace Fluxo.Infrastructure.Repositories;
public sealed class EfTelemetryPointWriter(FluxoDbContext context) : ITelemetryPointWriter
{
    public async Task WriteAsync(IReadOnlyCollection<TelemetryPoint> points, CancellationToken cancellationToken = default)
    { if (points.Count > 0) await context.TelemetryPoints.AddRangeAsync(points, cancellationToken); }
}
