using Fluxo.Domain.Enums;

namespace Fluxo.Worker.Ingestion.Options;

public class RejectionReprocessingOptions
{
    public const string SectionName = "RejectionReprocessing";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 120;
    public int BatchSize { get; set; } = 200;
    public int MaxAttempts { get; set; } = 5;

    /// Only these failure types are auto-retried. PayloadInvalid/Duplicate are excluded by
    /// default since malformed JSON or an already-persisted sequence won't succeed on retry.
    public List<TelemetryIngestionFailureType> ErrorTypes { get; set; } =
    [
        TelemetryIngestionFailureType.TransientError,
        TelemetryIngestionFailureType.DatabaseError
    ];
}
