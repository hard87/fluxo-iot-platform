using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace Fluxo.IntegrationTests.Infrastructure;

/// <summary>
/// Isolates the integration-test host from machine/user-scoped Windows resources.
///
/// The API never configures Data Protection or logging itself: <c>AddAuthentication</c>
/// implicitly calls <c>AddDataProtection()</c>, and <c>WebApplication.CreateBuilder</c>
/// adds the Event Log provider on Windows. Left alone, the test host therefore reads the
/// developer's DPAPI-protected key ring under %LOCALAPPDATA% and writes to the Windows
/// Event Log, which makes the suite depend on the account it runs under.
///
/// Everything here applies to the test host only. Nothing in src/ is modified, so the
/// application's production security configuration is untouched.
/// </summary>
internal static class TestHostIsolation
{
    private const string ApplicationDiscriminator = "fluxo-integration-tests";

    private static readonly Lazy<string> KeyDirectory = new(() =>
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "fluxo-tests-dataprotection",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    });

    /// <summary>
    /// Directory holding the key ring for this test process. Asserted by
    /// <c>TestHostIsolationTests</c> to prove the user's key ring is not used.
    /// </summary>
    public static string DataProtectionKeysPath => KeyDirectory.Value;

    /// <summary>
    /// Points Data Protection at a throwaway per-process directory and disables at-rest
    /// encryption, so no DPAPI call is made and the user's key ring is never read.
    /// </summary>
    /// <remarks>
    /// <c>UseEphemeralDataProtectionProvider()</c> is deliberately NOT used: it replaces
    /// only <see cref="IDataProtectionProvider"/>, leaving the <c>IKeyRingProvider</c>
    /// that <c>DataProtectionHostedService</c> eagerly resolves at host start still bound
    /// to the DPAPI-backed file-system repository.
    /// </remarks>
    public static IServiceCollection IsolateDataProtection(this IServiceCollection services)
    {
        services
            .AddDataProtection()
            .SetApplicationName(ApplicationDiscriminator)
            .PersistKeysToFileSystem(new DirectoryInfo(DataProtectionKeysPath));

        // PersistKeysToFileSystem sets XmlRepository; this sets XmlEncryptor. On Windows the
        // default would be DpapiXmlEncryptor, which is exactly what we are avoiding.
        services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new NullXmlEncryptor());

        return services;
    }

    /// <summary>
    /// Removes the Windows Event Log logger provider, keeping every other provider so test
    /// diagnostics remain visible.
    /// </summary>
    public static IServiceCollection RemoveEventLogLogging(this IServiceCollection services)
    {
        var eventLogProviders = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ILoggerProvider) &&
                descriptor.ImplementationType == typeof(EventLogLoggerProvider))
            .ToList();

        foreach (var descriptor in eventLogProviders)
        {
            services.Remove(descriptor);
        }

        return services;
    }
}
