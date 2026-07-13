namespace Fluxo.Infrastructure.Telemetry;
public sealed class TelemetryPointWriterOptions
{
    public const string SectionName = "TelemetryPointWriter";
    public string Strategy { get; set; } = "EfCore";
}
