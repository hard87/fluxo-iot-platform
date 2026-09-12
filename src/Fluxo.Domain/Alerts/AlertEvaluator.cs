namespace Fluxo.Domain.Alerts;

public static class AlertEvaluator
{
    public static string? Observe(AlertRuleRevision rule, AlertRuleState state,
        DateTime occurred, long sequence, Guid ingestionId, double? numeric, bool? boolean)
    {
        if (state.LastObservedAtUtc is { } last &&
            (occurred < last || (occurred == last && (sequence < state.LastSequence ||
            (sequence == state.LastSequence && string.CompareOrdinal(ingestionId.ToString("N"), state.LastIngestionRecordId.ToString("N")) <= 0)))))
            return "Historical";

        if (state.LastObservedAtUtc is { } prior &&
            (occurred - prior).TotalSeconds > Math.Max(2L * rule.ExpectedIntervalSeconds, rule.DurationSeconds))
            state.FirstViolationAtUtc = null;
        state.LastObservedAtUtc = occurred;
        state.LastSequence = sequence;
        state.LastIngestionRecordId = ingestionId;
        state.LastNumericValue = numeric;
        state.LastBooleanValue = boolean;

        var firing = Matches(rule, numeric, boolean);
        if (state.ActiveEventId is not null)
        {
            if (Resolved(rule, numeric, boolean, firing))
            {
                state.FirstViolationAtUtc = null;
                return "Resolved";
            }
            return null;
        }
        if (!firing) { state.FirstViolationAtUtc = null; return null; }
        state.FirstViolationAtUtc ??= occurred;
        if ((occurred - state.FirstViolationAtUtc.Value).TotalSeconds < rule.DurationSeconds) return null;
        if (state.LastTriggeredAtUtc is { } triggered && (occurred - triggered).TotalSeconds < rule.CooldownSeconds) return null;
        state.LastTriggeredAtUtc = occurred;
        return "Firing";
    }

    private static bool Matches(AlertRuleRevision r, double? n, bool? b) => r.Operator switch
    {
        "GreaterThan" => n > r.Threshold, "GreaterOrEqual" => n >= r.Threshold,
        "LessThan" => n < r.Threshold, "LessOrEqual" => n <= r.Threshold,
        "InsideRange" => n >= r.Threshold && n <= r.ThresholdHigh,
        "OutsideRange" => n < r.Threshold || n > r.ThresholdHigh,
        "IsTrue" => b == true, "IsFalse" => b == false,
        _ => throw new InvalidOperationException("Unsupported alert operator.")
    };

    private static bool Resolved(AlertRuleRevision r, double? n, bool? b, bool matches)
    {
        if (r.Hysteresis == 0 || b.HasValue) return !matches;
        return r.Operator switch
        {
            "GreaterThan" or "GreaterOrEqual" => n <= r.Threshold - r.Hysteresis,
            "LessThan" or "LessOrEqual" => n >= r.Threshold + r.Hysteresis,
            "OutsideRange" => n >= r.Threshold + r.Hysteresis && n <= r.ThresholdHigh - r.Hysteresis,
            "InsideRange" => n < r.Threshold - r.Hysteresis || n > r.ThresholdHigh + r.Hysteresis,
            _ => false
        };
    }
}
