namespace Fluxo.Application.Options;

public class DeviceStatusOptions
{
    public const string SectionName = "DeviceStatus";

    public int OfflineAfterSeconds { get; set; } = 120;
}
