namespace Fluxo.Domain.Enums;

public enum TelemetryIngestionFailureType
{
    PayloadInvalid = 1,
    Validation = 2,
    Duplicate = 3,
    DatabaseError = 4,
    TransientError = 5,
    ProcessingError = 6,
    MetricCardinalityGuardTriggered = 7,
    MetricTypeMismatch = 8,
    MetricWorkspaceLimitTriggered = 9
}
