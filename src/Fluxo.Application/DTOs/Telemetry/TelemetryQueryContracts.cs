namespace Fluxo.Application.DTOs.Telemetry;

public sealed record TelemetryQueryRequest(IReadOnlyList<Guid> DeviceIds, IReadOnlyList<string> MetricKeys,
    DateTime FromUtc, DateTime ToUtc, string Aggregation, string? Bucket);
public sealed record TelemetryQueryResponse(Guid WorkspaceId, DateTime FromUtc, DateTime ToUtc, string Aggregation,
    string? Bucket, IReadOnlyList<TelemetrySeriesResponse> Series, TelemetryQueryMeta Meta);
public sealed record TelemetrySeriesResponse(Guid DeviceId, string MetricKey, string ValueType, string? CanonicalUnit,
    string? SemanticType, IReadOnlyList<TelemetryPointResponse> Points, bool Truncated);
public sealed record TelemetryPointResponse(DateTime TimestampUtc, double? NumericValue, bool? BooleanValue,
    string? TextValue, int? SampleCount);
public sealed record TelemetryQueryMeta(int TotalPoints, int MaxPointsAllowed, long ExecutionTimeMs);
public sealed record MetricDefinitionResponse(Guid Id, string MetricKey, string DisplayName, string ValueType,
    string? SemanticType, string? CanonicalUnit, string Status, bool IsQueryable);
