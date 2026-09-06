using Npgsql;

namespace Fluxo.IntegrationTests.Infrastructure;

// One instance per admin connection. After a restart the old session no longer owns
// a lock: probe it before reuse, reconnect and revalidate the marker on the new session.
internal sealed class DisposableServerLease(string adminConnectionString) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private NpgsqlConnection? connection;

    public async Task EnsureAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (connection is not null)
            {
                try
                {
                    await using var probe = new NpgsqlCommand("SELECT 1", connection);
                    await probe.ExecuteScalarAsync();
                    return;
                }
                catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
                {
                    await connection.DisposeAsync();
                    connection = null;
                }
            }

            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Pooling = false,
                ApplicationName = $"fluxo-tests-run-{DisposableTestDatabase.RunId}"
            };
            var candidate = new NpgsqlConnection(builder.ConnectionString);
            try
            {
                await candidate.OpenAsync();
                await using var acquire = new NpgsqlCommand(
                    "SELECT pg_advisory_lock_shared(hashtext(@key))", candidate);
                acquire.Parameters.AddWithValue("key", DisposableTestDatabase.ServerLeaseKey);
                await acquire.ExecuteNonQueryAsync();

                await using var marker = new NpgsqlCommand(
                    "SELECT to_regclass('public.fluxo_disposable_test_marker') IS NOT NULL", candidate);
                if (await marker.ExecuteScalarAsync() is not true)
                    throw new InvalidOperationException("Disposable test marker was not found; refusing test server use.");
                connection = candidate;
            }
            catch
            {
                await candidate.DisposeAsync();
                throw;
            }
        }
        finally { gate.Release(); }
    }

    internal int BackendProcessId => connection?.ProcessID
        ?? throw new InvalidOperationException("Lease has not been acquired.");

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (connection is not null) await connection.DisposeAsync();
            connection = null;
        }
        finally { gate.Release(); }
    }
}
