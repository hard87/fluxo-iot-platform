using Fluxo.Domain.Alerts;

namespace Fluxo.UnitTests.Alerts;

public sealed class AlertEvaluatorTests
{
    [Theory]
    [InlineData("GreaterThan", 10, false)]
    [InlineData("GreaterThan", 11, true)]
    [InlineData("GreaterOrEqual", 10, true)]
    [InlineData("LessThan", 10, false)]
    [InlineData("LessThan", 9, true)]
    [InlineData("LessOrEqual", 10, true)]
    [InlineData("InsideRange", 10, true)]
    [InlineData("InsideRange", 20, true)]
    [InlineData("InsideRange", 21, false)]
    [InlineData("OutsideRange", 10, false)]
    [InlineData("OutsideRange", 20, false)]
    [InlineData("OutsideRange", 21, true)]
    [InlineData("OutsideRange", 9, true)]
    public void NumericBoundaries(string op, double value, bool fires)
    {
        var r = new AlertRuleRevision { Operator = op, Threshold = 10, ThresholdHigh = 20, ExpectedIntervalSeconds = 300 };
        var result = AlertEvaluator.Observe(r, new(), DateTime.UtcNow, 1, Guid.NewGuid(), value, null);
        Assert.Equal(fires ? "Firing" : null, result);
    }

    [Theory]
    [InlineData("GreaterOrEqual", 0, 10, false)]
    [InlineData("GreaterOrEqual", 0, 9, true)]
    [InlineData("GreaterThan", 2, 9, false)]
    [InlineData("GreaterThan", 2, 8, true)]
    [InlineData("LessThan", 2, 11, false)]
    [InlineData("LessThan", 2, 12, true)]
    [InlineData("InsideRange", 2, 8, false)]
    [InlineData("InsideRange", 2, 7, true)]
    [InlineData("OutsideRange", 2, 11, false)]
    [InlineData("OutsideRange", 2, 12, true)]
    public void RecoveryBoundaries(string op, double hysteresis, double value, bool resolves)
    {
        var r = new AlertRuleRevision { Operator = op, Threshold = 10, ThresholdHigh = 20, Hysteresis = hysteresis, ExpectedIntervalSeconds = 300 };
        var state = new AlertRuleState { ActiveEventId = Guid.NewGuid() };
        var result = AlertEvaluator.Observe(r, state, DateTime.UtcNow, 1, Guid.NewGuid(), value, null);
        Assert.Equal(resolves ? "Resolved" : null, result);
    }

    [Theory]
    [InlineData("IsTrue", true)]
    [InlineData("IsFalse", false)]
    public void BooleanOppositeResolves(string op, bool value)
    {
        var r = new AlertRuleRevision { Operator = op, ExpectedIntervalSeconds = 300 };
        var state = new AlertRuleState(); var at = DateTime.UtcNow;
        Assert.Equal("Firing", AlertEvaluator.Observe(r, state, at, 1, Guid.NewGuid(), null, value));
        state.ActiveEventId = Guid.NewGuid();
        Assert.Equal("Resolved", AlertEvaluator.Observe(r, state, at.AddSeconds(1), 2, Guid.NewGuid(), null, !value));
    }

    [Fact]
    public void TemporalTiesUseSequenceThenStableUuid()
    {
        var r = new AlertRuleRevision { Operator = "GreaterThan", Threshold = 10 };
        var state = new AlertRuleState(); var at = DateTime.UtcNow;
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Assert.Null(AlertEvaluator.Observe(r, state, at, 2, first, 5, null));
        Assert.Equal("Historical", AlertEvaluator.Observe(r, state, at, 1, second, 50, null));
        Assert.Equal("Firing", AlertEvaluator.Observe(r, state, at, 2, second, 50, null));
        Assert.Equal("Historical", AlertEvaluator.Observe(r, state, at, 2, second, 5, null));
        Assert.Equal(50, state.LastNumericValue);
    }
}
