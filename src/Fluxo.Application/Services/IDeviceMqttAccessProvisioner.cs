namespace Fluxo.Application.Services;

public interface IDeviceMqttAccessProvisioner
{
    Task ProvisionAsync(
        Guid deviceId,
        string username,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default);

    Task RotateAsync(
        Guid deviceId,
        string previousUsername,
        string newUsername,
        string plainSecret,
        string publishTopic,
        CancellationToken cancellationToken = default);
}
