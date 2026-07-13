using System.Diagnostics;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Telemetry;

public sealed class QueryTelemetryUseCase(
    GetAuthorizedWorkspaceUseCase authorizeWorkspace,
    GetAuthorizedDevicesUseCase authorizeDevices,
    ResolveMetricDefinitionsUseCase resolveMetrics,
    ITelemetryQueryRepository repository)
{
    public const int MaxPoints = 20_000;
    private static readonly Dictionary<string, TimeSpan> Buckets = new(StringComparer.Ordinal)
    {
        ["1m"] = TimeSpan.FromMinutes(1), ["5m"] = TimeSpan.FromMinutes(5),
        ["15m"] = TimeSpan.FromMinutes(15), ["1h"] = TimeSpan.FromHours(1),
        ["6h"] = TimeSpan.FromHours(6), ["1d"] = TimeSpan.FromDays(1)
    };
    private static readonly HashSet<string> Aggregations = ["raw", "avg", "min", "max", "sum", "count", "last"];

    public async Task<TelemetryQueryResponse> ExecuteAsync(Guid userId, Guid workspaceId,
        TelemetryQueryRequest request, CancellationToken ct)
    {
        ValidateShape(request);
        var workspace = await authorizeWorkspace.ExecuteAsync(userId, workspaceId, WorkspaceMembershipRole.Viewer, ct);
        var devices = await authorizeDevices.ExecuteAsync(workspace.Id, request.DeviceIds, ct);
        var metrics = await resolveMetrics.ExecuteAsync(workspace.Id, request.MetricKeys, ct);
        ValidateCompatibility(metrics.Select(x => (x.MetricKey, x.ValueType)), request.Aggregation);

        var seriesCount = devices.Count * metrics.Count;
        var rawLimit = MaxPoints / seriesCount;
        ValidateEstimatedPoints(request, seriesCount);

        var sw = Stopwatch.StartNew();
        var output = new List<TelemetrySeriesResponse>(seriesCount);
        foreach (var device in devices)
        foreach (var metric in metrics)
        {
            var points = await repository.QuerySeriesAsync(workspace.Id, device, metric, request.FromUtc,
                request.ToUtc, request.Aggregation, request.Bucket, rawLimit, ct);
            output.Add(new(device.Id, metric.MetricKey, metric.ValueType.ToString(), metric.CanonicalUnit,
                metric.SemanticType, points, request.Aggregation == "raw" && points.Count >= rawLimit));
        }
        sw.Stop();
        return new(workspace.Id, request.FromUtc, request.ToUtc, request.Aggregation, request.Bucket,
            output, new(output.Sum(x => x.Points.Count), MaxPoints, sw.ElapsedMilliseconds));
    }

    public static void ValidateShape(TelemetryQueryRequest request)
    {
        if (request.DeviceIds is null || request.DeviceIds.Count == 0) Fail("DEVICE_IDS_REQUIRED", "At least one device is required.");
        if (request.MetricKeys is null || request.MetricKeys.Count == 0) Fail("METRIC_KEYS_REQUIRED", "At least one metric is required.");
        var deviceIds = request.DeviceIds!;
        var metricKeys = request.MetricKeys!;
        if (deviceIds.Count > 10) Fail("TOO_MANY_DEVICES", "A maximum of 10 devices is allowed.");
        if (metricKeys.Count > 10) Fail("TOO_MANY_METRICS", "A maximum of 10 metrics is allowed.");
        if (deviceIds.Count * metricKeys.Count > 25) Fail("TOO_MANY_SERIES", "A maximum of 25 series is allowed.");
        if (request.FromUtc.Kind != DateTimeKind.Utc || request.ToUtc.Kind != DateTimeKind.Utc || request.FromUtc >= request.ToUtc)
            Fail("INVALID_RANGE", "fromUtc and toUtc must be UTC and fromUtc must precede toUtc.");
        if (!Aggregations.Contains(request.Aggregation)) Fail("AGGREGATION_NOT_ALLOWED", "Aggregation is not allowed.");
        var range = request.ToUtc - request.FromUtc;
        if (request.Aggregation == "raw")
        {
            if (request.Bucket is not null) Fail("BUCKET_NOT_ALLOWED_FOR_RAW", "Bucket must be null for raw queries.");
            if (range > TimeSpan.FromHours(24)) Fail("RANGE_TOO_LARGE_FOR_RAW", "Raw queries are limited to 24 hours.");
            return;
        }
        if (range > TimeSpan.FromDays(90)) Fail("RANGE_TOO_LARGE_FOR_AGGREGATE", "Aggregate queries are limited to 90 days.");
        if (request.Bucket is null) Fail("BUCKET_REQUIRED_FOR_AGGREGATE", "Bucket is required for aggregate queries.");
        if (!Buckets.TryGetValue(request.Bucket!, out var bucket)) Fail("BUCKET_NOT_ALLOWED", "Bucket is not allowed.");
        var minimum = range <= TimeSpan.FromHours(6) ? "1m" : range <= TimeSpan.FromHours(24) ? "5m" :
            range <= TimeSpan.FromDays(7) ? "15m" : range <= TimeSpan.FromDays(30) ? "1h" : "6h";
        if (bucket < Buckets[minimum]) Fail("BUCKET_BELOW_MINIMUM", $"Minimum bucket for this period is {minimum}.");
    }

    public static void ValidateCompatibility(IEnumerable<(string Key, MetricValueType Type)> metrics, string aggregation)
    {
        if (aggregation is "raw" or "count" or "last") return;
        var invalid = metrics.FirstOrDefault(x => x.Type != MetricValueType.Numeric);
        if (invalid != default) Fail("AGGREGATION_INCOMPATIBLE_WITH_VALUE_TYPE",
            $"Aggregation '{aggregation}' is incompatible with metric '{invalid.Key}'.");
    }

    public static void ValidateEstimatedPoints(TelemetryQueryRequest request, int seriesCount)
    {
        if (request.Aggregation == "raw") return;
        var estimate = (long)Math.Ceiling((request.ToUtc - request.FromUtc).TotalMilliseconds /
            Buckets[request.Bucket!].TotalMilliseconds) * seriesCount;
        if (estimate > MaxPoints)
            Fail("ESTIMATED_POINTS_EXCEEDS_LIMIT", $"The query may return {estimate} points; maximum is {MaxPoints}.");
    }

    private static void Fail(string code, string message) => throw new TelemetryQueryValidationException(code, message);
}
