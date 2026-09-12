using Npgsql;

namespace Fluxo.IntegrationTests.Infrastructure;

/// <summary>
/// Negative tests for the server lease.
///
/// RunId scopes databases, so one run can never drop another run's database. It says nothing
/// about the server's lifecycle: a `docker compose down` from one run would still destroy a
/// server another run is actively using. The lease closes that gap, and these tests prove it
/// by asserting that teardown's own precondition fails while a run is active.
/// </summary>
public sealed class DisposableServerLeaseTests
{
    [SkippableFact]
    public async Task LostSession_ReacquiresLeaseBeforeReuse()
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        var admin = Environment.GetEnvironmentVariable(DisposableTestDatabase.AdminConnectionVariable)!;
        await using var lease = new DisposableServerLease(admin);
        await lease.EnsureAsync();
        var oldPid = lease.BackendProcessId;
        await using var observer = new NpgsqlConnection(admin);
        await observer.OpenAsync();
        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", observer);
        terminate.Parameters.AddWithValue("pid", oldPid);
        Assert.True((bool)(await terminate.ExecuteScalarAsync())!);

        await lease.EnsureAsync();
        Assert.NotEqual(oldPid, lease.BackendProcessId);
        await using var locks = new NpgsqlCommand(
            "SELECT count(*) FROM pg_locks WHERE pid = @pid AND locktype = 'advisory' AND granted AND mode = 'ShareLock'", observer);
        locks.Parameters.AddWithValue("pid", lease.BackendProcessId);
        Assert.Equal(1L, (long)(await locks.ExecuteScalarAsync())!);
    }

    [SkippableFact]
    public async Task LostSession_RevalidatesMarkerBeforeReuse()
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        await DisposableTestDatabase.WithDatabaseAsync("it", async admin =>
        {
            await using var observer = new NpgsqlConnection(admin);
            await observer.OpenAsync();
            await using var create = new NpgsqlCommand(
                "CREATE TABLE public.fluxo_disposable_test_marker (id integer)", observer);
            await create.ExecuteNonQueryAsync();
            await using var lease = new DisposableServerLease(admin);
            await lease.EnsureAsync();
            await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", observer);
            terminate.Parameters.AddWithValue("pid", lease.BackendProcessId);
            await terminate.ExecuteScalarAsync();
            await using var drop = new NpgsqlCommand("DROP TABLE public.fluxo_disposable_test_marker", observer);
            await drop.ExecuteNonQueryAsync();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lease.EnsureAsync());
            Assert.Contains("marker", error.Message);
        });
    }

    [SkippableFact]
    public async Task ActiveRun_Prevents_Exclusive_Lease_Acquisition()
    {
        DisposableTestDatabase.SkipUnlessAvailable();

        var admin = Environment.GetEnvironmentVariable(
            DisposableTestDatabase.AdminConnectionVariable)!;

        await DisposableTestDatabase.EnsureRunLeaseAsync(admin);

        // Exactly what stop-test-postgres.ps1 evaluates before tearing the server down.
        await using var observer = new NpgsqlConnection(admin);
        await observer.OpenAsync();

        await using var command = observer.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@key))";
        command.Parameters.AddWithValue("key", DisposableTestDatabase.ServerLeaseKey);

        var acquired = (bool)(await command.ExecuteScalarAsync())!;

        Assert.False(
            acquired,
            "A teardown must not be able to take the exclusive lease while this run holds it. " +
            "If this passes, stop-test-postgres.ps1 would destroy a server in use.");
    }

    [SkippableFact]
    public async Task ActiveRun_Is_Attributable_To_Its_RunId()
    {
        DisposableTestDatabase.SkipUnlessAvailable();

        var admin = Environment.GetEnvironmentVariable(
            DisposableTestDatabase.AdminConnectionVariable)!;

        await DisposableTestDatabase.EnsureRunLeaseAsync(admin);

        // Refusing teardown is only actionable if the operator can see who is holding it.
        await using var observer = new NpgsqlConnection(admin);
        await observer.OpenAsync();

        await using var command = observer.CreateCommand();
        command.CommandText = """
            SELECT count(*)
            FROM pg_locks l
            JOIN pg_stat_activity a ON a.pid = l.pid
            WHERE l.locktype = 'advisory'
              AND l.granted
              AND a.application_name = @name
            """;
        command.Parameters.AddWithValue("name", $"fluxo-tests-run-{DisposableTestDatabase.RunId}");

        var holders = Convert.ToInt64(await command.ExecuteScalarAsync());

        Assert.True(
            holders > 0,
            $"The lease held by run '{DisposableTestDatabase.RunId}' must be attributable in pg_stat_activity.");
    }

    [SkippableFact]
    public async Task Foreign_RunId_Database_Name_Is_Refused()
    {
        DisposableTestDatabase.SkipUnlessAvailable();

        // A well-formed name belonging to a different run must still be refused, so a run can
        // never drop a concurrent run's database.
        var foreignName = $"fluxo_it_{new string('a', 8)}_{new string('b', 32)}";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DisposableTestDatabase.DropForTestingAsync(
                Environment.GetEnvironmentVariable(DisposableTestDatabase.AdminConnectionVariable)!,
                foreignName));

        Assert.Contains("another test run", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
