using Fluxo.IntegrationTests.Infrastructure;
using Fluxo.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fluxo.IntegrationTests.Api;

public class FluxoWebApplicationFactory : WebApplicationFactory<Program>
{
    // Never connected to: AddInfrastructure only needs a parseable connection string at
    // startup, and the DbContext below is replaced with the in-memory provider before any
    // query runs. Port 1 makes an accidental connection attempt fail instantly instead of
    // reaching a real server -- the previous value was localhost:5432, which could hit
    // whatever happened to be listening there.
    private const string UnusedConnectionString =
        "Host=localhost;Port=1;Database=fluxo_tests_unused;Username=unused;Password=unused";

    private const string TestSigningKey =
        "tests-signing-key-32-chars-minimum-0123456789abcdef";

    // These must be process-wide environment variables, not just host configuration.
    // Program.cs calls AddInfrastructure/AddFluxoSecurity while the host is being built by
    // DeferredHostBuilder, which happens BEFORE ConfigureAppConfiguration below is applied;
    // both throw at that point if the connection string or signing key is missing.
    // Set once per process rather than per factory instance.
    static FluxoWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection", UnusedConnectionString);
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey", TestSigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = UnusedConnectionString,
                ["Authentication:Jwt:SigningKey"] = TestSigningKey
            });
        });

        builder.ConfigureServices(services =>
        {
            // Keeps the host off the developer's DPAPI key ring and the Windows Event Log.
            services.IsolateDataProtection();
            services.RemoveEventLogLogging();

            services.RemoveAll(typeof(DbContextOptions<FluxoDbContext>));
            services.RemoveAll(typeof(FluxoDbContext));

            var databaseName = $"fluxo-tests-{Guid.NewGuid()}";
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<FluxoDbContext>(options =>
                options
                    .UseInMemoryDatabase(databaseName)
                    .UseInternalServiceProvider(inMemoryProvider));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }
}
