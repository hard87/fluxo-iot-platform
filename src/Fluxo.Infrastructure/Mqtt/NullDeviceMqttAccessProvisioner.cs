using Fluxo.Application.Services;

namespace Fluxo.Infrastructure.Mqtt;

/// Used when MqttDynamicSecurity:Enabled is false (tests, or operators who still manage
/// broker credentials manually). Keeps device provisioning working without a broker connection.
public sealed class NullDeviceMqttAccessProvisioner : IDeviceMqttAccessProvisioner
{
    public Task ProvisionAsync(
        Guid deviceId,
        string username,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RotateAsync(
        Guid deviceId,
        string previousUsername,
        string newUsername,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
