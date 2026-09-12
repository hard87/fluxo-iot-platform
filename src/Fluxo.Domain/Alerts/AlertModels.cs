namespace Fluxo.Domain.Alerts;

public sealed class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? CurrentRevisionId { get; set; }
    public int Version { get; set; }
}

public sealed class AlertRuleRevision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid RuleId { get; init; }
    public int Version { get; init; }
    public string Name { get; init; } = "";
    public Guid MetricDefinitionId { get; init; }
    public string? DeviceIdentifier { get; init; }
    public string ValueType { get; init; } = "";
    public string? Unit { get; init; }
    public string Operator { get; init; } = "";
    public double? Threshold { get; init; }
    public double? ThresholdHigh { get; init; }
    public double Hysteresis { get; init; }
    public int DurationSeconds { get; init; }
    public int CooldownSeconds { get; init; }
    public int ExpectedIntervalSeconds { get; init; }
    public string Severity { get; init; } = "Warning";
    public bool Enabled { get; init; }
    public DateTime ActivatedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid AuthorId { get; init; }
}

public sealed class AlertDeviceCoordination
{
    public Guid WorkspaceId { get; set; }
    public string DeviceIdentifier { get; set; } = "";
    public long NextOrder { get; set; }
}

public sealed class AlertEvaluationWorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string DeviceIdentifier { get; set; } = "";
    public Guid IngestionRecordId { get; set; }
    public long QueueOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
}

public sealed class AlertEvaluationAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string DeviceIdentifier { get; set; } = "";
    public Guid WorkItemId { get; set; }
    public long QueueOrder { get; set; }
    public Guid RuleId { get; set; }
    public Guid RevisionId { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? Reason { get; set; }
    public string? ClaimedBy { get; set; }
}

public sealed class AlertRuleState
{
    public Guid WorkspaceId { get; set; }
    public Guid RuleId { get; set; }
    public string DeviceIdentifier { get; set; } = "";
    public Guid RevisionId { get; set; }
    public DateTime? FirstViolationAtUtc { get; set; }
    public DateTime? LastObservedAtUtc { get; set; }
    public long LastSequence { get; set; }
    public Guid LastIngestionRecordId { get; set; }
    public double? LastNumericValue { get; set; }
    public bool? LastBooleanValue { get; set; }
    public DateTime? LastTriggeredAtUtc { get; set; }
    public Guid? ActiveEventId { get; set; }
}

public sealed class AlertEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid RuleId { get; init; }
    public Guid RevisionId { get; init; }
    public string DeviceIdentifier { get; init; } = "";
    public DateTime TriggeredAtUtc { get; init; }
    public string Status { get; set; } = "Firing";
    public int Ordinal { get; set; }
}

public sealed class AlertEventTransition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid EventId { get; init; }
    public int Ordinal { get; init; }
    public string Kind { get; init; } = "";
    public DateTime OccurredAtUtc { get; init; }
    public DateTime RecordedAtUtc { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public Guid? IngestionRecordId { get; init; }
    public double? NumericValue { get; init; }
    public bool? BooleanValue { get; init; }
    public string Reason { get; init; } = "";
    public string EvaluatorVersion { get; init; } = "alerts-v1";
}

public sealed class AlertAcknowledgement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid EventId { get; init; }
    public Guid AuthorId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

// Channel-neutral durable intent. Stage 3 will resolve subscriptions and create
// delivery attempts; this record never authorizes or performs external transport.
public sealed class AlertDeliveryIntent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid TransitionId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
