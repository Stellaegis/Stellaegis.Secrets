using Npgsql;

namespace Stellaegis.Secrets.Tests;

/// <summary>
/// A dedicated PostgreSQL database for the vault tests, its <c>vault</c> schema rebuilt from the
/// repository's DDL at every run.
/// </summary>
/// <remarks>
/// The connection string comes from <c>STELLAEGIS_SECRETS_TEST_POSTGRES</c>, defaulting to a local
/// server and the database <c>stellaegis_secrets_tests</c>, created if missing. When no server answers,
/// <see cref="Unavailable"/> says why, and the tests that need it are skipped rather than failed.
/// </remarks>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string Variable = "STELLAEGIS_SECRETS_TEST_POSTGRES";
    private const string Default  = "Host=127.0.0.1;Username=postgres;Database=stellaegis_secrets_tests";

    /// <summary>The connection string to the test database.</summary>
    public string ConnectionString { get; } = Environment.GetEnvironmentVariable(Variable) ?? Default;

    /// <summary>Why the database cannot be used, or <c>null</c> when it can.</summary>
    public string? Unavailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var target = new NpgsqlConnectionStringBuilder(ConnectionString);
        var server = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };

        try
        {
            await using (var connection = new NpgsqlConnection(server.ConnectionString))
            {
                await connection.OpenAsync();
                await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
                exists.Parameters.AddWithValue("name", target.Database!);
                if (await exists.ExecuteScalarAsync() is null)
                {
                    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\"", connection);
                    await create.ExecuteNonQueryAsync();
                }
            }

            var ddl = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "stellaegis_secrets.sql"));
            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var reset = new NpgsqlCommand("DROP SCHEMA IF EXISTS vault CASCADE;\n" + ddl, connection);
                await reset.ExecuteNonQueryAsync();
            }
        }
        catch (NpgsqlException e)
        {
            Unavailable = $"No PostgreSQL for the vault tests ({Variable} or '{Default}'): {e.Message}";
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
