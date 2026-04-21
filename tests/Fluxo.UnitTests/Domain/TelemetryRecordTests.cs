using Fluxo.Domain.Entities;

namespace Fluxo.UnitTests.Domain;

public class TelemetryRecordTests
{
    [Fact]
    public void Should_Create_Telemetry_When_Payload_Is_Valid()
    {
        var telemetry = new TelemetryRecord(
            Guid.NewGuid(),
            "{\"temperature\":22.4,\"humidity\":60.2}",
            DateTime.UtcNow.AddMinutes(-1));

        Assert.NotEqual(Guid.Empty, telemetry.Id);
        Assert.Equal(DateTimeKind.Utc, telemetry.OccurredAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, telemetry.IngestedAtUtc.Kind);
        Assert.Contains("\"temperature\":22.4", telemetry.PayloadJson);
    }

    [Fact]
    public void Should_Throw_When_DeviceId_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new TelemetryRecord(Guid.Empty, "{\"a\":1}"));
    }

    [Fact]
    public void Should_Throw_When_Payload_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new TelemetryRecord(Guid.NewGuid(), string.Empty));
    }
}
