using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Npgsql;

namespace Fluxo.IntegrationTests.Infrastructure;

/// <summary>
/// Creates and drops throwaway PostgreSQL databases for relational integration tests.
///
/// Replaces three near-identical private helpers that each read
/// FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION and silently <c>return</c> when it was missing,
/// which made the tests report as passed without executing anything.
///
/// Every destructive statement is gated behind guards that refuse any target not proven to
/// be a disposable test server. See docker/test/docker-compose.tests.yml.
/// </summary>
internal static class DisposableTestDatabase
{
    /// <summary>Admin connection string for the disposable test server.</summary>
    public const string AdminConnectionVariable = "FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION";

    /// <summary>When set to 1/true, a missing test server fails instead of skipping.</summary>
    public const string RequirePostgresVariable = "FLUXO_TESTS_REQUIRE_POSTGRES";

    /// <summary>
    /// Table the disposable server must expose. Created by docker/test/init/00-marker.sql.
    /// A production or pilot database will not have it, so pointing the variable at one is
    /// refused before any DDL runs.
    /// </summary>
    public const string MarkerTable = "fluxo_disposable_test_marker";

    private static readonly string[] AllowedHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>Databases that must never be touched even if the marker is somehow present.</summary>
    private static readonly string[] ForbiddenDatabases = ["fluxo_db", "postgres", "template0", "template1"];

    /// <summary>
    /// Identifies this test process on a server that may be shared with other runs. Embedded
    /// in every database name so ownership is visible in pg_database and enforceable below.
    /// </summary>
    public static readonly string RunId = Guid.NewGuid().ToString("N")[..8];

    private static readonly Regex DisposableNamePattern =
        new("^fluxo_(it|query|v2)_[0-9a-f]{8}_[0-9a-f]{32}$", RegexOptions.Compiled);

    /// <summary>
    /// Databases actually created by this process. Dropping is restricted to these, so a run
    /// can never remove a database belonging to a concurrent run on the same server.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> OwnedDatabases = new(StringComparer.Ordinal);

    /// <summary>
    /// Advisory-lock key naming the whole disposable server. Every active run holds it in
    /// SHARED mode for its entire lifetime; a teardown must acquire it EXCLUSIVELY first.
    /// That is what makes "is anyone using this server?" answerable, which RunId alone could
    /// not do: RunId scopes databases, not the server's lifecycle.
    /// </summary>
    public const string ServerLeaseKey = "fluxo:tests:server-lease";

    private static readonly ConcurrentDictionary<string, DisposableServerLease> Leases = new();

    /// <summary>
    /// The reason relational tests cannot run, or <c>null</c> when the server is usable.
    /// Callers pass this to <c>Skip.If</c> so an unavailable server is reported as skipped,
    /// never as passed.
    /// </summary>
    public static string? UnavailableReason()
    {
        var admin = Environment.GetEnvironmentVariable(AdminConnectionVariable);

        if (string.IsNullOrWhiteSpace(admin))
        {
            return $"{AdminConnectionVariable} is not set. Start the disposable test server " +
                   "with scripts/tests/start-test-postgres.ps1 and export the variable it prints.";
        }

        return null;
    }

    /// <summary>
    /// Skips the calling test when no disposable server is configured, so it is reported as
    /// skipped rather than passed. When <see cref="RequirePostgresVariable"/> is set the
    /// missing server fails the test instead, which is how the official baseline is run.
    /// </summary>
    public static void SkipUnlessAvailable()
    {
        var reason = UnavailableReason();

        if (reason is null)
        {
            return;
        }

        if (PostgresIsRequired())
        {
            throw new InvalidOperationException(
                $"{RequirePostgresVariable} is set, so relational coverage is mandatory, but {reason}");
        }

        Skip.If(true, reason);
    }

    /// <summary>
    /// True when the operator demanded a real relational run, turning an unavailable server
    /// into a failure rather than a skip.
    /// </summary>
    public static bool PostgresIsRequired()
    {
        var value = Environment.GetEnvironmentVariable(RequirePostgresVariable);

        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a disposable database, hands its connection string to <paramref name="test"/>
    /// and drops it afterwards. Only databases this call created are dropped.
    /// </summary>
    /// <param name="prefix">One of <c>it</c>, <c>query</c> or <c>v2</c>.</param>
    public static async Task WithDatabaseAsync(string prefix, Func<string, Task> test)
    {
        var admin = Environment.GetEnvironmentVariable(AdminConnectionVariable);

        if (string.IsNullOrWhiteSpace(admin))
        {
            throw new InvalidOperationException(
                $"{AdminConnectionVariable} is not set. Call Skip.If(DisposableTestDatabase.UnavailableReason() is not null) first.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(admin);
        GuardAdminTarget(adminBuilder);
        await GuardDisposableServerAsync(adminBuilder);
        await EnsureRunLeaseAsync(adminBuilder.ConnectionString);

        var databaseName = $"fluxo_{prefix}_{RunId}_{Guid.NewGuid():N}";
        GuardDisposableDatabaseName(databaseName);

        var testBuilder = new NpgsqlConnectionStringBuilder(admin) { Database = databaseName };

        await CreateDatabaseAsync(adminBuilder.ConnectionString, databaseName);

        try
        {
            await test(testBuilder.ConnectionString);
        }
        finally
        {
            await DropDatabaseAsync(adminBuilder.ConnectionString, databaseName);
        }
    }

    /// <summary>
    /// Registers this run as an active user of the disposable server by taking a SHARED
    /// advisory lock that is held until the process exits.
    ///
    /// Concurrent runs coexist (shared mode), but a teardown -- stop-test-postgres.ps1 --
    /// must first acquire the same key EXCLUSIVELY, so it can detect and refuse to remove a
    /// server another run is still using. Losing the process releases the lock immediately,
    /// so an aborted run cannot block teardown forever.
    /// </summary>
    public static async Task EnsureRunLeaseAsync(string adminConnectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        GuardAdminTarget(builder);
        await Leases.GetOrAdd(builder.ConnectionString,
            static value => new DisposableServerLease(value)).EnsureAsync();
    }

    /// <summary>
    /// Cheap, offline guards: the target must be a local host and must not name a database
    /// we know belongs to a real environment.
    /// </summary>
    private static void GuardAdminTarget(NpgsqlConnectionStringBuilder builder)
    {
        var host = builder.Host ?? string.Empty;

        if (!AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run disposable-database tests against host '{host}'. " +
                $"Only {string.Join(", ", AllowedHosts)} are allowed.");
        }

        var database = builder.Database ?? string.Empty;

        if (ForbiddenDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to use '{database}' as the admin database for disposable tests. " +
                "Point " + AdminConnectionVariable + " at the disposable test server " +
                "(docker/test/docker-compose.tests.yml), not at a real environment.");
        }
    }

    /// <summary>
    /// The decisive guard: asks the server itself whether it is disposable. String matching
    /// alone cannot tell a test server from the pilot, so the server must carry the marker
    /// table created by docker/test/init/00-marker.sql.
    /// </summary>
    private static async Task GuardDisposableServerAsync(NpgsqlConnectionStringBuilder builder)
    {
        var hasMarker = await HasMarkerAsync(builder.ConnectionString);

        if (!hasMarker)
        {
            throw new InvalidOperationException(
                $"Refusing to create or drop databases on this server: table public.{MarkerTable} " +
                "was not found, so it is not identifiable as a disposable test environment. " +
                "Start it with scripts/tests/start-test-postgres.ps1.");
        }
    }

    private static async Task<bool> HasMarkerAsync(string adminConnectionString)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.' || @marker) IS NOT NULL";
        command.Parameters.AddWithValue("marker", MarkerTable);

        return await command.ExecuteScalarAsync() is true;
    }

    /// <summary>
    /// Guarantees the name was generated by this helper, so the quoted identifier below can
    /// never be anything but a throwaway database.
    /// </summary>
    private static void GuardDisposableDatabaseName(string databaseName)
    {
        if (!DisposableNamePattern.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                $"'{databaseName}' is not a generated disposable database name. Refusing to run DDL against it.");
        }

        if (!databaseName.Contains($"_{RunId}_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{databaseName}' belongs to another test run (this run is '{RunId}'). " +
                "Refusing to run DDL against it.");
        }
    }

    /// <summary>
    /// Refuses to drop anything this process did not create, even when the name is otherwise
    /// well formed. Concurrent runs share the server, so ownership is checked explicitly.
    /// </summary>
    private static void GuardOwnership(string databaseName)
    {
        if (!OwnedDatabases.ContainsKey(databaseName))
        {
            throw new InvalidOperationException(
                $"Refusing to drop '{databaseName}': it was not created by this run ('{RunId}').");
        }
    }

    /// <summary>
    /// Test seam: exercises the drop path's guards directly, so the refusal to touch another
    /// run's database can be asserted without fabricating one.
    /// </summary>
    internal static Task DropForTestingAsync(string adminConnectionString, string databaseName) =>
        DropDatabaseAsync(adminConnectionString, databaseName);

    private static async Task CreateDatabaseAsync(string adminConnectionString, string databaseName)
    {
        GuardDisposableDatabaseName(databaseName);

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();

        // Registered only after CREATE succeeded, so a failed create never authorises a drop.
        OwnedDatabases[databaseName] = 0;
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        GuardDisposableDatabaseName(databaseName);
        GuardOwnership(databaseName);

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var terminate = connection.CreateCommand();
        terminate.CommandText =
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @name AND pid <> pg_backend_pid()";
        terminate.Parameters.AddWithValue("name", databaseName);
        await terminate.ExecuteNonQueryAsync();

        await using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await drop.ExecuteNonQueryAsync();

        OwnedDatabases.TryRemove(databaseName, out _);
    }
}
