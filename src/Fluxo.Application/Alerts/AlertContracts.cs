using Fluxo.Domain.Alerts;

namespace Fluxo.Application.Alerts;

public sealed record SaveAlertRuleRequest(string Name, Guid MetricDefinitionId, string? DeviceIdentifier,
    string Operator, double? Threshold = null, double? ThresholdHigh = null, double Hysteresis = 0,
    int DurationSeconds = 0, int CooldownSeconds = 0, string Severity = "Warning", bool Enabled = false,
    int? ExpectedVersion = null);

public sealed record AlertHistory(AlertEvent Event, AlertRuleRevision Revision,
    IReadOnlyList<AlertEventTransition> Transitions, IReadOnlyList<AlertAcknowledgement> Acknowledgements,
    IReadOnlyList<AlertDeliveryIntent> DeliveryIntents);

public interface IAlertManagement
{
    Task<AlertRuleRevision> SaveAsync(Guid userId, Guid workspaceId, Guid? ruleId, SaveAlertRuleRequest request, CancellationToken ct);
    Task<IReadOnlyList<AlertRuleRevision>> RulesAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct);
    Task<IReadOnlyList<AlertRuleRevision>> RevisionsAsync(Guid userId, Guid workspaceId, Guid ruleId, int page, CancellationToken ct);
    Task<IReadOnlyList<AlertEvent>> EventsAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct);
    Task<AlertHistory> HistoryAsync(Guid userId, Guid workspaceId, Guid eventId, CancellationToken ct);
    Task<AlertAcknowledgement> AcknowledgeAsync(Guid userId, Guid workspaceId, Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<AlertAttemptDiagnostic>> DiagnosticsAsync(Guid userId, Guid workspaceId, int page, CancellationToken ct);
}

public sealed record AlertAttemptDiagnostic(Guid Id, Guid WorkItemId, Guid RuleId, string DeviceIdentifier,
    string Status, int AttemptCount, DateTime? NextAttemptAtUtc, string? Reason);

public sealed class AlertEvaluationOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int WorkItemLeaseSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 5;
    public int RetryBaseSeconds { get; set; } = 5;
    public int DefaultExpectedIntervalSec { get; set; } = 300;
}
