namespace Fluxo.Worker.Ingestion.Options;

public class MqttIngestionOptions
{
    public const string SectionName = "MqttIngestion";

    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "fluxo-worker-ingestion";
    public string TopicFilter { get; set; } = "fluxo/tenants/+/workspaces/+/devices/+/telemetry";
    public int ReconnectDelaySeconds { get; set; } = 5;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
    public string? TlsTargetHost { get; set; }
    public string? TlsCaCertificatePath { get; set; }
    public bool TlsAllowUntrustedCertificates { get; set; }
    public bool TlsIgnoreCertificateChainErrors { get; set; }
    public bool TlsIgnoreCertificateRevocationErrors { get; set; }
    public int ChannelCapacity { get; set; } = 5000;
    public int ProcessingConcurrency { get; set; } = 4;
    public int DatabaseRetryCount { get; set; } = 3;
    public int DatabaseRetryDelayMs { get; set; } = 200;
}
