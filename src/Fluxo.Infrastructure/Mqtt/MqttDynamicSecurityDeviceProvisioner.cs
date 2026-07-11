using Fluxo.Application.Services;
using Microsoft.Extensions.Logging;

namespace Fluxo.Infrastructure.Mqtt;

/// Grants/revokes per-device MQTT access on the broker's dynamic-security plugin at the
/// exact moment the API knows the plaintext credential secret (provision/rotate), so no
/// manual password_file/acl_file sync step is required. See docs/mqtt-tls-e-credenciais.md.
public sealed class MqttDynamicSecurityDeviceProvisioner : IDeviceMqttAccessProvisioner
{
    private readonly DynamicSecurityControlClient _controlClient;
    private readonly ILogger<MqttDynamicSecurityDeviceProvisioner> _logger;

    public MqttDynamicSecurityDeviceProvisioner(
        DynamicSecurityControlClient controlClient,
        ILogger<MqttDynamicSecurityDeviceProvisioner> logger)
    {
        _controlClient = controlClient;
        _logger = logger;
    }

    public async Task ProvisionAsync(
        Guid deviceId,
        string username,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default)
    {
        var roleName = BuildRoleName(deviceId);

        await _controlClient.SendCommandAsync(
            "createRole",
            new Dictionary<string, object?>
            {
                ["rolename"] = roleName,
                ["textname"] = $"Fluxo device {deviceId}",
                ["acls"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["acltype"] = "publishClientSend",
                        ["topic"] = publishTopic,
                        ["priority"] = -1,
                        ["allow"] = true
                    }
                }
            },
            cancellationToken);

        await CreateClientAsync(username, plainSecret, roleName, cancellationToken);

        _logger.LogInformation(
            "Acesso MQTT provisionado no broker para device {DeviceId} (usuario {Username}).",
            deviceId,
            username);
    }

    public async Task RotateAsync(
        Guid deviceId,
        string previousUsername,
        string newUsername,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default)
    {
        var roleName = BuildRoleName(deviceId);

        await CreateClientAsync(newUsername, plainSecret, roleName, cancellationToken);

        try
        {
            await _controlClient.SendCommandAsync(
                "deleteClient",
                new Dictionary<string, object?> { ["username"] = previousUsername },
                cancellationToken);
        }
        catch (DynamicSecurityCommandException ex)
        {
            _logger.LogWarning(
                ex,
                "Nao foi possivel remover a credencial MQTT anterior ({PreviousUsername}) do device {DeviceId} apos rotacao. " +
                "A nova credencial ja esta ativa; remova manualmente se necessario.",
                previousUsername,
                deviceId);
        }

        _logger.LogInformation(
            "Acesso MQTT rotacionado no broker para device {DeviceId} (novo usuario {Username}).",
            deviceId,
            newUsername);
    }

    private async Task CreateClientAsync(
        string username,
        string plainSecret,
        string roleName,
        CancellationToken cancellationToken)
    {
        await _controlClient.SendCommandAsync(
            "createClient",
            new Dictionary<string, object?>
            {
                ["username"] = username,
                ["password"] = plainSecret,
                ["textname"] = "Fluxo device credential",
                ["roles"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["rolename"] = roleName,
                        ["priority"] = -1
                    }
                }
            },
            cancellationToken);
    }

    private static string BuildRoleName(Guid deviceId) => $"device-{deviceId:N}";
}
