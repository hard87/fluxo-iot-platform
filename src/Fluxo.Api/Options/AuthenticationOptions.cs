namespace Fluxo.Api.Options;

public sealed class AuthenticationOptions
{
    public bool Enabled { get; set; }
    public JwtOptions Jwt { get; set; } = new();
}

public sealed class JwtOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
}
