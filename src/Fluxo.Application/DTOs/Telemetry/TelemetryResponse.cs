namespace Fluxo.Application.DTOs.Telemetry;

public class TelemetryResponse
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; }
    public DateTime IngestedAtUtc { get; set; }
}
