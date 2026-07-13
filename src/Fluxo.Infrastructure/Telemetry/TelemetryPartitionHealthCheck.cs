using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Fluxo.Infrastructure.Telemetry;
public sealed class TelemetryPartitionHealthCheck(TelemetryPartitionState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lastMaintenanceFailed = state.LastFailureUtc.HasValue &&
                                    (!state.LastSuccessUtc.HasValue || state.LastFailureUtc > state.LastSuccessUtc);
        var result = lastMaintenanceFailed ? HealthCheckResult.Unhealthy("Last telemetry partition maintenance failed.")
            : !state.NextMonthExists ? HealthCheckResult.Unhealthy("Next month's telemetry partition is missing.")
            : state.PartitionsAhead < 3 ? HealthCheckResult.Degraded("Fewer than three future telemetry partitions exist.")
            : HealthCheckResult.Healthy($"{state.PartitionsAhead} future partitions available.");
        return Task.FromResult(result);
    }
}
