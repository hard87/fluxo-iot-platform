using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Fluxo.Api.Options;
using Fluxo.Application.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fluxo.Api.Services;

public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly AuthenticationOptions _authOptions;

    public JwtAccessTokenService(IOptions<AuthenticationOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public AccessTokenIssueResult Issue(AccessTokenPayload payload)
    {
        var signingKey = ResolveSigningKey(_authOptions.Jwt.SigningKey);
        var issuer = string.IsNullOrWhiteSpace(_authOptions.Jwt.Issuer)
            ? "fluxo-local"
            : _authOptions.Jwt.Issuer.Trim();
        var audience = string.IsNullOrWhiteSpace(_authOptions.Jwt.Audience)
            ? "fluxo-api"
            : _authOptions.Jwt.Audience.Trim();
        var expiresMinutes = Math.Clamp(_authOptions.Jwt.ExpiresMinutes, 5, 24 * 60);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, payload.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, payload.Email),
            new(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new(ClaimTypes.Email, payload.Email)
        };

        claims.AddRange(payload.WorkspaceIds.Select(workspaceId => new Claim("workspace", workspaceId)));

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenIssueResult(tokenValue, expiresAtUtc);
    }

    private static SymmetricSecurityKey ResolveSigningKey(string? signingKey)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(signingKey)
            ? "change-this-local-signing-key-with-32-chars-min"
            : signingKey.Trim();

        if (normalizedKey.Length < 32)
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey must contain at least 32 characters.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(normalizedKey));
    }
}
