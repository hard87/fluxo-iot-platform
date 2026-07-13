using Fluxo.Infrastructure.Mqtt;
using Microsoft.Extensions.Options;

namespace Fluxo.Api.Services;

/// Ensures the telemetry-ingestion worker has a broker identity on startup, so the manual
/// "add ingestion worker to passwords/acl" step documented previously is no longer required.
/// Idempotent: safe to run on every API start (dynamic-security.json persists across restarts).
public sealed class MqttIngestionWorkerAccessBootstrapper : IHostedService
{
    private const string RoleName = "ingestion-worker";

    private readonly IServiceProvider _serviceProvider;
    private readonly MqttDynamicSecurityOptions _options;
    private readonly ILogger<MqttIngestionWorkerAccessBootstrapper> _logger;

    public MqttIngestionWorkerAccessBootstrapper(
        IServiceProvider serviceProvider,
        IOptions<MqttDynamicSecurityOptions> options,
        ILogger<MqttIngestionWorkerAccessBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        var username = _options.IngestionWorker.Username;
        var password = _options.IngestionWorker.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "MqttDynamicSecurity esta habilitado mas MqttDynamicSecurity:IngestionWorker:Username/Password " +
                "nao foram configurados. O worker de ingestao nao tera acesso automatico ao broker.");
            return;
        }

        try
        {
            var controlClient = _serviceProvider.GetRequiredService<DynamicSecurityControlClient>();

            try
            {
                await controlClient.SendCommandAsync(
                    "createRole",
                    new Dictionary<string, object?>
                    {
                        ["rolename"] = RoleName,
                        ["textname"] = "Fluxo telemetry ingestion worker",
                        ["acls"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["acltype"] = "subscribePattern",
                                ["topic"] = _options.IngestionWorker.TopicFilter,
                                ["priority"] = -1,
                                ["allow"] = true
                            },
                            new Dictionary<string, object?>
                            {
                                ["acltype"] = "publishClientReceive",
                                ["topic"] = _options.IngestionWorker.TopicFilter,
                                ["priority"] = -1,
                                ["allow"] = true
                            }
                        }
                    },
                    cancellationToken);
            }
            catch (DynamicSecurityCommandException ex)
            {
                _logger.LogInformation(
                    "Role '{RoleName}' provavelmente ja existe no broker ({Message}); prosseguindo.",
                    RoleName,
                    ex.Message);
            }

            await controlClient.SendCommandAsync(
                "createClient",
                new Dictionary<string, object?>
                {
                    ["username"] = username,
                    ["password"] = password,
                    ["textname"] = "Fluxo ingestion worker",
                    ["roles"] = new[]
                    {
                        new Dictionary<string, object?> { ["rolename"] = RoleName, ["priority"] = -1 }
                    }
                },
                cancellationToken);

            _logger.LogInformation("Acesso MQTT do worker de ingestao garantido no broker.");
        }
        catch (DynamicSecurityCommandException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Cliente MQTT '{Username}' do worker de ingestao ja existe no broker; prosseguindo.",
                username);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao garantir acesso MQTT do worker de ingestao no broker. " +
                "A API vai iniciar normalmente, mas o worker pode nao conseguir conectar ate a proxima tentativa.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
