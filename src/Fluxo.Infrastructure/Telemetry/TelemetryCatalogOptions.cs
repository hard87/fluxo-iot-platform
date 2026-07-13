namespace Fluxo.Infrastructure.Telemetry;
public sealed class TelemetryCatalogOptions
{
    public const string SectionName = "TelemetryCatalog";
    public int MaxMetricDefinitionsPerWorkspace { get; set; } = 500;
    public int MaxNewMetricKeysPerDevicePerHour { get; set; } = 20;
}
