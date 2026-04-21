using System.ComponentModel.DataAnnotations;

namespace Fluxo.Application.DTOs.Telemetry;

public class CreateTelemetryRequest
{
    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    [MinLength(1)]
    public string PayloadJson { get; set; } = "{}";

    public DateTime? OccurredAtUtc { get; set; }
}
