using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Fluxo.Application.UseCases.Provisioning;

internal static partial class DeviceProvisioningConventions
{
    private const int UsernameMaxLength = 120;

    public static string BuildMqttPublishTopic(string tenantId, Guid workspaceId, string deviceIdentifier)
    {
        return $"fluxo/tenants/{tenantId}/workspaces/{workspaceId}/devices/{deviceIdentifier}/telemetry";
    }

    public static string BuildCredentialUsername(string tenantId, Guid workspaceId, string deviceIdentifier)
    {
        var tenantToken = NormalizeToken(tenantId);
        var deviceToken = NormalizeToken(deviceIdentifier);
        var workspaceToken = workspaceId.ToString("N")[..8];
        var baseUsername = $"dev-{tenantToken}-{workspaceToken}-{deviceToken}";

        if (baseUsername.Length <= UsernameMaxLength)
            return baseUsername;

        var hash = ComputeShortHash(baseUsername);
        var prefixLength = UsernameMaxLength - hash.Length - 1;
        var prefix = baseUsername[..Math.Max(prefixLength, 1)];
        return $"{prefix}-{hash}";
    }

    private static string NormalizeToken(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        var sanitized = InvalidTokenChars().Replace(lowered, "-");
        sanitized = MultipleHyphenChars().Replace(sanitized, "-").Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "device" : sanitized;
    }

    private static string ComputeShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    [GeneratedRegex("[^a-z0-9\\-_]+", RegexOptions.Compiled)]
    private static partial Regex InvalidTokenChars();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphenChars();
}
