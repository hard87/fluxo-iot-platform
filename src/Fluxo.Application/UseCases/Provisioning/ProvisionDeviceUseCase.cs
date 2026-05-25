using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Provisioning;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Options;
using Fluxo.Application.Services;
using Fluxo.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Fluxo.Application.UseCases.Provisioning;

public sealed class ProvisionDeviceUseCase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCredentialRepository _credentialRepository;
    private readonly IDeviceCredentialSecretService _secretService;
    private readonly TimeSpan _offlineAfter;

    public ProvisionDeviceUseCase(
        IDeviceRepository deviceRepository,
        IDeviceCredentialRepository credentialRepository,
        IDeviceCredentialSecretService secretService,
        IOptions<DeviceStatusOptions> deviceStatusOptions)
    {
        _deviceRepository = deviceRepository;
        _credentialRepository = credentialRepository;
        _secretService = secretService;
        var configuredSeconds = deviceStatusOptions.Value.OfflineAfterSeconds;
        _offlineAfter = TimeSpan.FromSeconds(Math.Max(configuredSeconds, 30));
    }

    public async Task<ProvisionedDeviceResponse> ExecuteAsync(
        ProvisionDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkspaceId == Guid.Empty)
            throw new ValidationException("WorkspaceId is required.");

        var normalizedTenantId = Device.NormalizeTenantId(request.TenantId);
        var normalizedIdentifier = request.Identifier.Trim();

        var existingDevice = await _deviceRepository.GetByTenantWorkspaceAndIdentifierAsync(
            normalizedTenantId,
            request.WorkspaceId,
            normalizedIdentifier,
            cancellationToken);

        if (existingDevice is not null)
            throw new ConflictException("A device with this tenant/workspace/identifier already exists.");

        var device = new Device(
            request.WorkspaceId,
            request.Name,
            normalizedIdentifier,
            request.Category,
            request.MetadataJson,
            normalizedTenantId);

        await _deviceRepository.AddAsync(device, cancellationToken);

        var username = await GenerateUniqueUsernameAsync(
            normalizedTenantId,
            request.WorkspaceId,
            normalizedIdentifier,
            cancellationToken);

        var secretMaterial = _secretService.Create();
        var credential = new DeviceCredential(
            device.Id,
            username,
            secretMaterial.SecretHash,
            secretMaterial.SecretSalt);

        await _credentialRepository.AddAsync(credential, cancellationToken);

        return new ProvisionedDeviceResponse
        {
            DeviceId = device.Id,
            TenantId = device.TenantId,
            WorkspaceId = device.WorkspaceId,
            DeviceName = device.Name,
            DeviceIdentifier = device.Identifier,
            DeviceCategory = device.Category,
            DeviceIsActive = device.IsActive,
            DeviceCreatedAtUtc = device.CreatedAtUtc,
            LastContactAtUtc = device.LastContactAtUtc,
            OperationalStatus = device.GetOperationalStatus(DateTime.UtcNow, _offlineAfter),
            CredentialId = credential.Id,
            CredentialUsername = credential.Username,
            CredentialStatus = credential.Status,
            CredentialCreatedAtUtc = credential.CreatedAtUtc,
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
