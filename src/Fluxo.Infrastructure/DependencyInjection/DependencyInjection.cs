using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Services;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.Mqtt;
using Fluxo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxo.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Database connection is not configured. Set ConnectionStrings__DefaultConnection or Database settings.");

        services.AddDbContext<FluxoDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDeviceCredentialRepository, DeviceCredentialRepository>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceMembershipRepository, WorkspaceMembershipRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<ITelemetryIngestionRepository, TelemetryIngestionRepository>();
        services.AddScoped<ITelemetryIngestionRejectionRepository, TelemetryIngestionRejectionRepository>();

        AddMqttDynamicSecurity(services, configuration);

        return services;
    }

    private static void AddMqttDynamicSecurity(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MqttDynamicSecurityOptions.SectionName);
        services.Configure<MqttDynamicSecurityOptions>(section);

        var options = section.Get<MqttDynamicSecurityOptions>() ?? new MqttDynamicSecurityOptions();

        if (options.Enabled)
        {
            services.AddSingleton<DynamicSecurityControlClient>();
            services.AddSingleton<IDeviceMqttAccessProvisioner, MqttDynamicSecurityDeviceProvisioner>();
        }
        else
        {
            services.AddSingleton<IDeviceMqttAccessProvisioner, NullDeviceMqttAccessProvisioner>();
        }
    }

    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var host = ReadSetting(configuration, "Database:Host", "FLUXO_DB_HOST");
        var database = ReadSetting(configuration, "Database:Name", "FLUXO_DB_NAME");
        var username = ReadSetting(configuration, "Database:Username", "FLUXO_DB_USERNAME");
        var password = ReadSetting(configuration, "Database:Password", "FLUXO_DB_PASSWORD");
        var portText = ReadSetting(configuration, "Database:Port", "FLUXO_DB_PORT");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Username = username,
            Password = password
        };

        if (int.TryParse(portText, out var port) && port > 0)
            builder.Port = port;

        return builder.ConnectionString;
    }

    private static string? ReadSetting(IConfiguration configuration, string key, string envVarName)
    {
        var configValue = configuration[key];
        if (!string.IsNullOrWhiteSpace(configValue))
            return configValue;

        var envValue = Environment.GetEnvironmentVariable(envVarName);
        return string.IsNullOrWhiteSpace(envValue) ? null : envValue;
    }
}
