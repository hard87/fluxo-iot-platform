using System.Security.Cryptography;
using System.Text;

namespace Fluxo.Application.Services;

public interface IUserPasswordService
{
    PasswordHashMaterial HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash, string passwordSalt);
}

public sealed record PasswordHashMaterial(string PasswordHash, string PasswordSalt);

public sealed class UserPasswordService : IUserPasswordService
{
    private const int SaltSize = 16;
    private const int DerivedKeySize = 32;
    private const int Iterations = 210_000;

    public PasswordHashMaterial HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.", nameof(password));

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Derive(password, saltBytes);

        return new PasswordHashMaterial(
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string passwordHash, string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(passwordHash) ||
            string.IsNullOrWhiteSpace(passwordSalt))
        {
            return false;
        }

        byte[] expectedHashBytes;
        byte[] saltBytes;

        try
        {
            expectedHashBytes = Convert.FromBase64String(passwordHash);
            saltBytes = Convert.FromBase64String(passwordSalt);
        }
        catch (FormatException)
        {
            return false;
        }

        var candidateHashBytes = Derive(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(candidateHashBytes, expectedHashBytes);
    }

    private static byte[] Derive(string password, byte[] saltBytes)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            DerivedKeySize);
    }
}
