using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Fluxo.Infrastructure.Data;

public sealed class FluxoDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FluxoDbContext>
{
    public FluxoDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionStringFromEnvironment();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection is not configured for EF design-time. " +
                "Set ConnectionStrings__DefaultConnection or FLUXO_DB_HOST/NAME/USERNAME/PASSWORD.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<FluxoDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FluxoDbContext(optionsBuilder.Options);
    }

    private static string? ResolveConnectionStringFromEnvironment()
    {
        var directConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(directConnection))
            return directConnection;

        var host = ReadEnvironment("FLUXO_DB_HOST");
        var database = ReadEnvironment("FLUXO_DB_NAME");
        var username = ReadEnvironment("FLUXO_DB_USERNAME");
        var password = ReadEnvironment("FLUXO_DB_PASSWORD");
        var portText = ReadEnvironment("FLUXO_DB_PORT");

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

    private static string? ReadEnvironment(string envVarName)
    {
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        return string.IsNullOrWhiteSpace(envValue) ? null : envValue;
    }
}
