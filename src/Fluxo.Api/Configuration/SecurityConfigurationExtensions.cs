using Fluxo.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Fluxo.Api.Configuration;

public static class SecurityConfigurationExtensions
{
    public static IServiceCollection AddFluxoSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authSection = configuration.GetSection("Authentication");
        services.Configure<AuthenticationOptions>(authSection);

        var authOptions = authSection.Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = authOptions.Jwt.RequireHttpsMetadata;

                if (authOptions.Enabled && !string.IsNullOrWhiteSpace(authOptions.Jwt.Authority))
                {
                    options.Authority = authOptions.Jwt.Authority;
                    options.Audience = authOptions.Jwt.Audience;
                    return;
                }

                var audience = string.IsNullOrWhiteSpace(authOptions.Jwt.Audience)
                    ? "fluxo-api"
                    : authOptions.Jwt.Audience.Trim();

                var issuer = string.IsNullOrWhiteSpace(authOptions.Jwt.Issuer)
                    ? "fluxo-local"
                    : authOptions.Jwt.Issuer.Trim();

                var signingKey = string.IsNullOrWhiteSpace(authOptions.Jwt.SigningKey)
                    ? "change-this-local-signing-key-with-32-chars-min"
                    : authOptions.Jwt.SigningKey.Trim();

                if (signingKey.Length < 32)
                {
                    throw new InvalidOperationException(
                        "Authentication:Jwt:SigningKey must contain at least 32 characters.");
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
        return services;
    }
}
