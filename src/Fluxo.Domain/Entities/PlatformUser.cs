using System.Text.RegularExpressions;

namespace Fluxo.Domain.Entities;

public sealed partial class PlatformUser
{
    private PlatformUser()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public PlatformUser(
        string email,
        string passwordHash,
        string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (!EmailPattern().IsMatch(email.Trim()))
            throw new ArgumentException("Email is invalid.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));

        if (string.IsNullOrWhiteSpace(passwordSalt))
            throw new ArgumentException("PasswordSalt is required.", nameof(passwordSalt));

        Id = Guid.NewGuid();
        Email = email.Trim();
        EmailNormalized = NormalizeEmail(email);
        PasswordHash = passwordHash.Trim();
        PasswordSalt = passwordSalt.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordLogin(DateTime loggedAtUtc)
    {
        LastLoginAtUtc = EnsureUtc(loggedAtUtc, nameof(loggedAtUtc));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return email.Trim().ToUpperInvariant();
    }

    private static DateTime EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        throw new ArgumentException("DateTime must include a UTC or Local kind.", paramName);
    }

    [GeneratedRegex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();
}
