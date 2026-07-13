using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Models;

public sealed record TelemetryMetricValue(string Key, MetricValueType ValueType, double? NumericValue = null, bool? BooleanValue = null, string? TextValue = null);
