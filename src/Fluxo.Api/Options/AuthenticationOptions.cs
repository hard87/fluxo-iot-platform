namespace Fluxo.Api.Options;

public sealed class AuthenticationOptions
{
    public bool Enabled { get; set; }
    public JwtOptions Jwt { get; set; } = new();
}

public sealed class JwtOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; } = "fluxo-api";
    public string Issuer { get; set; } = "fluxo-local";
    public string SigningKey { get; set; } = "change-this-local-signing-key-with-32-chars-min";
    public int ExpiresMinutes { get; set; } = 60;
    public bool RequireHttpsMetadata { get; set; } = true;
}
