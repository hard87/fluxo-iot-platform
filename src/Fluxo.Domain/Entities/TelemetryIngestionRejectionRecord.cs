using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class TelemetryIngestionRejectionRecord
{
    private TelemetryIngestionRejectionRecord()
    {
    }

    public Guid Id { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string PayloadRaw { get; private set; } = string.Empty;
    public TelemetryIngestionFailureType ErrorType { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? TenantId { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public string? DeviceId { get; private set; }
    public string? MessageType { get; private set; }
    public long? Sequence { get; private set; }

    /// When set, this row is the outcome of a reprocess attempt of another rejection and is
    /// excluded from future reprocessing eligibility — only "root" ingestion failures are
    /// auto-retried, so retries can't fan out into retrying their own retries.
    public Guid? SourceRejectionId { get; private set; }
    public bool Reprocessed { get; private set; }
    public int ReprocessAttempts { get; private set; }
    public DateTime? LastReprocessAttemptAtUtc { get; private set; }

    public TelemetryIngestionRejectionRecord(
        DateTime receivedAtUtc,
        string topic,
        string payloadRaw,
        TelemetryIngestionFailureType errorType,
        string reason,
        string? tenantId = null,
        Guid? workspaceId = null,
        string? deviceId = null,
        string? messageType = null,
        long? sequence = null,
        Guid? sourceRejectionId = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (!Enum.IsDefined(errorType))
            throw new ArgumentException("ErrorType is invalid.", nameof(errorType));

        if (workspaceId.HasValue && workspaceId.Value == Guid.Empty)
            throw new ArgumentException("WorkspaceId cannot be empty when provided.", nameof(workspaceId));

        Id = Guid.NewGuid();
        ReceivedAtUtc = EnsureUtc(receivedAtUtc, nameof(receivedAtUtc));
        Topic = topic.Trim();
        PayloadRaw = payloadRaw ?? string.Empty;
        ErrorType = errorType;
        Reason = reason.Trim();
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        WorkspaceId = workspaceId;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        MessageType = string.IsNullOrWhiteSpace(messageType) ? null : messageType.Trim();
        Sequence = sequence;
        SourceRejectionId = sourceRejectionId;
    }

    public void RecordReprocessAttempt(DateTime attemptedAtUtc, bool resolved)
    {
        ReprocessAttempts++;
        LastReprocessAttemptAtUtc = EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));

        if (resolved)
            Reprocessed = true;
    }

    private static DateTime EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        throw new ArgumentException("DateTime must include a UTC or Local kind.", paramName);
    }
}
