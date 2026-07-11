namespace Fluxo.Infrastructure.Mqtt;

public class MqttDynamicSecurityOptions
{
    public const string SectionName = "MqttDynamicSecurity";

    public bool Enabled { get; set; }
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public string? TlsTargetHost { get; set; }
    public string? TlsCaCertificatePath { get; set; }
    public bool TlsAllowUntrustedCertificates { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 10;

    public IngestionWorkerOptions IngestionWorker { get; set; } = new();

    public class IngestionWorkerOptions
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string TopicFilter { get; set; } = "fluxo/tenants/+/workspaces/+/devices/+/telemetry";
    }
}
