using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Services;
using Fluxo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Fluxo.Application.UseCases.Provisioning;

public sealed class RotateDeviceCredentialUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCredentialRepository _credentialRepository;
    private readonly IDeviceCredentialSecretService _secretService;
    private readonly IDeviceMqttAccessProvisioner _mqttAccessProvisioner;
    private readonly ILogger<RotateDeviceCredentialUseCase> _logger;

    public RotateDeviceCredentialUseCase(
        IDeviceRepository deviceRepository,
        IDeviceCredentialRepository credentialRepository,
        IDeviceCredentialSecretService secretService,
        IDeviceMqttAccessProvisioner mqttAccessProvisioner,
        ILogger<RotateDeviceCredentialUseCase> logger)
    {
        _deviceRepository = deviceRepository;
        _credentialRepository = credentialRepository;
        _secretService = secretService;
        _mqttAccessProvisioner = mqttAccessProvisioner;
        _logger = logger;
    }

    public async Task<RotateDeviceCredentialResponse> ExecuteAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ValidationException("Device id is required.");

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
            throw new NotFoundException("Device not found.");

        var previousCredential = await _credentialRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);

        var username = await GenerateUniqueUsernameAsync(
            device.TenantId,
            device.WorkspaceId,
            device.Identifier,
            cancellationToken);

        var secretMaterial = _secretService.Create();
        var newCredential = new DeviceCredential(
            device.Id,
            username,
            secretMaterial.SecretHash,
            secretMaterial.SecretSalt);

        await _credentialRepository.RotateAsync(device.Id, newCredential, cancellationToken);

        var mqttPublishTopic = DeviceProvisioningConventions.BuildMqttPublishTopic(
            device.TenantId,
            device.WorkspaceId,
            device.Identifier);

        try
        {
            await _mqttAccessProvisioner.RotateAsync(
                device.Id,
                previousCredential?.Username ?? username,
                newCredential.Username,
                secretMaterial.PlainSecret,
                mqttPublishTopic,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao rotacionar acesso MQTT no broker para device {DeviceId}. " +
                "A nova credencial foi criada no banco, mas pode nao funcionar no broker ate sincronizacao manual.",
                device.Id);
        }

        return new RotateDeviceCredentialResponse
        {
            DeviceId = device.Id,
            CredentialId = newCredential.Id,
            CredentialUsername = newCredential.Username,
            CredentialStatus = newCredential.Status,
            CredentialCreatedAtUtc = newCredential.CreatedAtUtc,
            ProvisioningSecret = secretMaterial.PlainSecret,
            MqttPublishTopic = mqttPublishTopic
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(
        string tenantId,
        Guid workspaceId,
        string identifier,
        CancellationToken cancellationToken)
    {
        var baseUsername = DeviceProvisioningConventions.BuildCredentialUsername(
            tenantId,
            workspaceId,
            identifier);

        if (await _credentialRepository.GetByUsernameAsync(baseUsername, cancellationToken) is null)
            return baseUsername;

        for (var suffix = 1; suffix <= 1000; suffix++)
        {
            var candidate = $"{baseUsername}-{suffix}";
            if (candidate.Length > 120)
                candidate = candidate[..120];

            if (await _credentialRepository.GetByUsernameAsync(candidate, cancellationToken) is null)
                return candidate;
        }

        throw new ConflictException("Could not allocate a unique credential username for this device.");
    }
}
