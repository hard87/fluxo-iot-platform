namespace Fluxo.Application.DTOs.Portal;

public sealed record TelemetryRejectionItemResponse(
    Guid Id,
    DateTime ReceivedAtUtc,
    string Topic,
    string PayloadPreview,
    string ErrorType,
    string Reason,
    string? DeviceId,
    string? MessageType,
    long? Sequence,
    bool Reprocessed,
    int ReprocessAttempts);

public sealed record TelemetryRejectionPageResponse(
    IReadOnlyList<TelemetryRejectionItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);
