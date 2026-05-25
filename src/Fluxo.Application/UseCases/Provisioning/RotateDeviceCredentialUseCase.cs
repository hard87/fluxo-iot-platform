using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Services;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Provisioning;

public sealed class RotateDeviceCredentialUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCredentialRepository _credentialRepository;
    private readonly IDeviceCredentialSecretService _secretService;

    public RotateDeviceCredentialUseCase(
        IDeviceRepository deviceRepository,
        IDeviceCredentialRepository credentialRepository,
        IDeviceCredentialSecretService secretService)
    {
        _deviceRepository = deviceRepository;
        _credentialRepository = credentialRepository;
        _secretService = secretService;
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

        return new RotateDeviceCredentialResponse
        {
            DeviceId = device.Id,
            CredentialId = newCredential.Id,
            CredentialUsername = newCredential.Username,
            CredentialStatus = newCredential.Status,
            CredentialCreatedAtUtc = newCredential.CreatedAtUtc,
            ProvisioningSecret = secretMaterial.PlainSecret,
            MqttPublishTopic = DeviceProvisioningConventions.BuildMqttPublishTopic(
                device.TenantId,
                device.WorkspaceId,
                device.Identifier)
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
