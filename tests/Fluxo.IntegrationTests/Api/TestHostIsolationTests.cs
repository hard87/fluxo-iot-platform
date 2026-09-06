using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;

namespace Fluxo.IntegrationTests.Api;

/// <summary>
/// Guards the isolation itself. Without these, a future change could silently reintroduce
/// the dependency on the developer's Windows profile: the API configures neither Data
/// Protection nor logging, so both come from framework defaults and a regression would be
/// invisible until the suite is run under a different account.
/// </summary>
public class TestHostIsolationTests : IClassFixture<FluxoWebApplicationFactory>
{
    private readonly FluxoWebApplicationFactory _factory;

    public TestHostIsolationTests(FluxoWebApplicationFactory factory)
    {
        _factory = factory;
        _ = _factory.CreateClient();
    }

    [Fact]
    public void DataProtection_Should_Not_Use_The_Windows_User_Key_Ring()
    {
        var options = _factory.Services
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value;

        var repository = Assert.IsType<FileSystemXmlRepository>(options.XmlRepository);
        var userKeyRing = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ASP.NET",
            "DataProtection-Keys");

        Assert.Equal(
            Path.GetFullPath(TestHostIsolation.DataProtectionKeysPath),
            Path.GetFullPath(repository.Directory.FullName));

        Assert.False(
            Path.GetFullPath(repository.Directory.FullName)
                .StartsWith(Path.GetFullPath(userKeyRing), StringComparison.OrdinalIgnoreCase),
            "The test host must not read or write the developer's Data Protection key ring.");
    }

    [Fact]
    public void DataProtection_Should_Not_Encrypt_Keys_With_Dpapi()
    {
        var options = _factory.Services
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value;

        // Anything other than NullXmlEncryptor on Windows means a DPAPI call at key creation.
        Assert.IsType<NullXmlEncryptor>(options.XmlEncryptor);
    }

    [Fact]
    public void Logging_Should_Not_Write_To_The_Windows_Event_Log()
    {
        var providers = _factory.Services.GetServices<ILoggerProvider>();

        Assert.DoesNotContain(providers, provider => provider is EventLogLoggerProvider);
    }
}
