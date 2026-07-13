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
    public FluxoWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Host=localhost;Port=5432;Database=fluxo_tests;Username=fluxo;Password=fluxo");
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            "tests-signing-key-32-chars-minimum-0123456789abcdef");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=fluxo_tests;Username=fluxo;Password=fluxo",
                ["Authentication:Jwt:SigningKey"] =
                    "tests-signing-key-32-chars-minimum-0123456789abcdef"
            });
        });

        builder.ConfigureServices(services =>
        {
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
