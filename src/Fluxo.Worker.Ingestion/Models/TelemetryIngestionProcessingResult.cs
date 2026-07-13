namespace Fluxo.Worker.Ingestion.Models;

public sealed record TelemetryIngestionProcessingResult(
    TelemetryIngestionProcessingStatus Status,
    string Reason)
{
    public static TelemetryIngestionProcessingResult Persisted() =>
        new(TelemetryIngestionProcessingStatus.Persisted, "Telemetry persisted.");

    public static TelemetryIngestionProcessingResult Rejected(string reason) =>
        new(TelemetryIngestionProcessingStatus.Rejected, reason);

    public static TelemetryIngestionProcessingResult Duplicate(string reason) =>
        new(TelemetryIngestionProcessingStatus.Duplicate, reason);

    public static TelemetryIngestionProcessingResult DatabaseError(string reason) =>
        new(TelemetryIngestionProcessingStatus.DatabaseError, reason);

    public static TelemetryIngestionProcessingResult TransientFailure(string reason) =>
        new(TelemetryIngestionProcessingStatus.TransientFailure, reason);

    public static TelemetryIngestionProcessingResult ProcessingError(string reason) =>
        new(TelemetryIngestionProcessingStatus.ProcessingError, reason);
}
