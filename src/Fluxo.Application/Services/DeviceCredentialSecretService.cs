using System.Security.Cryptography;
using System.Text;

namespace Fluxo.Application.Services;

public interface IDeviceCredentialSecretService
{
    DeviceCredentialSecretMaterial Create();
}

public sealed record DeviceCredentialSecretMaterial(
    string PlainSecret,
    string SecretHash,
    string SecretSalt);

public sealed class DeviceCredentialSecretService : IDeviceCredentialSecretService
{
    private const int SaltSize = 16;
    private const int DerivedKeySize = 32;
    private const int Iterations = 100_000;

    public DeviceCredentialSecretMaterial Create()
    {
        var plainSecret = GenerateOpaqueSecret();
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainSecret),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            DerivedKeySize);

        return new DeviceCredentialSecretMaterial(
            plainSecret,
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes));
    }

    private static string GenerateOpaqueSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
