using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.UseCases.Telemetry;
using Fluxo.Domain.Enums;

namespace Fluxo.UnitTests.Telemetry;

public sealed class QueryTelemetryUseCaseTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(Guardrails))]
    public void ShapeGuardrails_ReturnExpectedErrorCode(TelemetryQueryRequest request, string expected)
    {
        var ex = Assert.Throws<TelemetryQueryValidationException>(() => QueryTelemetryUseCase.ValidateShape(request));
        Assert.Equal(expected, ex.ErrorCode);
    }

    public static IEnumerable<object[]> Guardrails()
    {
        yield return [Request(devices: 11), "TOO_MANY_DEVICES"];
        yield return [Request(metrics: 11), "TOO_MANY_METRICS"];
        yield return [Request(devices: 6, metrics: 5), "TOO_MANY_SERIES"];
        yield return [Request(hours: 25), "RANGE_TOO_LARGE_FOR_RAW"];
        yield return [Request(hours: 24*91, aggregation: "avg", bucket: "6h"), "RANGE_TOO_LARGE_FOR_AGGREGATE"];
        yield return [Request(aggregation: "raw", bucket: "1m"), "BUCKET_NOT_ALLOWED_FOR_RAW"];
        yield return [Request(aggregation: "avg"), "BUCKET_REQUIRED_FOR_AGGREGATE"];
    }

    [Theory]
    [InlineData(7, "1m", "5m")]
    [InlineData(25, "5m", "15m")]
    [InlineData(24*8, "15m", "1h")]
    [InlineData(24*31, "1h", "6h")]
    [InlineData(24*90, "1h", "6h")]
    public void BucketFloors_AreExact(int hours, string rejected, string minimum)
    {
        var ex = Assert.Throws<TelemetryQueryValidationException>(() =>
            QueryTelemetryUseCase.ValidateShape(Request(hours: hours, aggregation: "avg", bucket: rejected)));
        Assert.Equal("BUCKET_BELOW_MINIMUM", ex.ErrorCode);
        Assert.Contains(minimum, ex.Message);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void ValueTypeAggregationMatrix_IsExact(MetricValueType type, string aggregation, bool allowed)
    {
        var action = () => QueryTelemetryUseCase.ValidateCompatibility([("metric", type)], aggregation);
        if (allowed) action();
        else Assert.Equal("AGGREGATION_INCOMPATIBLE_WITH_VALUE_TYPE",
            Assert.Throws<TelemetryQueryValidationException>(action).ErrorCode);
    }

    public static IEnumerable<object[]> Matrix()
    {
        string[] aggregations = ["raw", "avg", "min", "max", "sum", "count", "last"];
        foreach (var type in Enum.GetValues<MetricValueType>())
        foreach (var aggregation in aggregations)
            yield return [type, aggregation, type == MetricValueType.Numeric || aggregation is "raw" or "count" or "last"];
    }

    [Fact]
    public void EstimatedPoints_RejectsBeforeRepositoryStage()
    {
        var request = Request(hours: 24*30, devices: 5, metrics: 5, aggregation: "avg", bucket: "15m");
        var ex = Assert.Throws<TelemetryQueryValidationException>(() => QueryTelemetryUseCase.ValidateEstimatedPoints(request, 25));
        Assert.Equal("ESTIMATED_POINTS_EXCEEDS_LIMIT", ex.ErrorCode);
    }

    private static TelemetryQueryRequest Request(int devices=1, int metrics=1, int hours=1,
        string aggregation="raw", string? bucket=null) => new(
        Enumerable.Range(0, devices).Select(_ => Guid.NewGuid()).ToArray(),
        Enumerable.Range(0, metrics).Select(i => $"metric_{i}").ToArray(), Start, Start.AddHours(hours), aggregation, bucket);
}
