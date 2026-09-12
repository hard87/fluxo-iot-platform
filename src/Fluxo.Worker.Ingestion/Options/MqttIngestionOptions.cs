namespace Fluxo.Worker.Ingestion.Options;

public class MqttIngestionOptions
{
    public const string SectionName = "MqttIngestion";

    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "fluxo-worker-ingestion";
    public string TopicFilter { get; set; } = "fluxo/tenants/+/workspaces/+/devices/+/telemetry";
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int KeepAliveSeconds { get; set; } = 300;

    /// <summary>
    /// MQTT 5: quanto tempo (segundos) o broker deve reter a sessão (assinatura + mensagens QoS>=1
    /// enfileiradas) após uma desconexão, antes de descartá-la. Sem isso (valor 0, o default do
    /// protocolo quando omitido), <c>WithCleanSession(false)</c> não tem efeito real em MQTT 5 --
    /// CleanStart=false só evita começar uma sessão nova, mas sem SessionExpiryInterval > 0 o
    /// broker expira a sessão no instante da desconexão de qualquer forma, e mensagens publicadas
    /// por outros clientes nesse intervalo (ex.: durante um restart do broker ou um reconnect deste
    /// worker) são aceitas (PUBACK) e descartadas sem entrega, silenciosamente. Confirmado em
    /// produção: ver `Fluxo/devices/edgewarden-test-harness/docs/test-plan.md`, achado de
    /// 2026-08-08 (2 e depois 3 sequences perdidas em dois drills de resiliência do gateway
    /// EdgeWarden, ambos com o mosquitto restartando com o worker offline por alguns segundos).
    /// Default de 1h: suficiente para cobrir reconexão após restart/manutenção do broker sem reter
    /// recursos indefinidamente para um worker genuinamente morto.
    /// </summary>
    public uint SessionExpiryIntervalSeconds { get; set; } = 3600;
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
    public int MaxPayloadBytes { get; set; } = 32768;
    public int MaxPastDays { get; set; } = 30;
}
