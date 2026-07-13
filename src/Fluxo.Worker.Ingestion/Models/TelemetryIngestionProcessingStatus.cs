namespace Fluxo.Worker.Ingestion.Models;

public enum TelemetryIngestionProcessingStatus
{
    Persisted = 1,
    Rejected = 2,
    Duplicate = 3,
    DatabaseError = 4,
    TransientFailure = 5,
    ProcessingError = 6
}
