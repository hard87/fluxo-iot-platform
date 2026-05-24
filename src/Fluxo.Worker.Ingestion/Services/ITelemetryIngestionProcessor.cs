using Fluxo.Worker.Ingestion.Models;

namespace Fluxo.Worker.Ingestion.Services;

public interface ITelemetryIngestionProcessor
{
    Task<TelemetryIngestionProcessingResult> ProcessAsync(
        string topic,
        string payloadJson,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
}
