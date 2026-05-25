using Fluxo.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

        var authenticationBuilder = services.AddAuthentication();

        if (authOptions.Enabled)
        {
            if (string.IsNullOrWhiteSpace(authOptions.Jwt.Authority) ||
                string.IsNullOrWhiteSpace(authOptions.Jwt.Audience))
            {
                throw new InvalidOperationException(
                    "Authentication is enabled but Jwt:Authority/Audience are missing.");
            }

            authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = authOptions.Jwt.RequireHttpsMetadata;
                options.Authority = authOptions.Jwt.Authority;
                options.Audience = authOptions.Jwt.Audience;
            });
        }

        services.AddAuthorization();
        return services;
    }
}
