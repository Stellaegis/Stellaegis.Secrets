using Npgsql;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Data;
using Stellaegis.Secrets.PostgreSQL;

namespace Stellaegis.Secrets.Tests;

/// <summary>
/// A dedicated PostgreSQL database for the vault tests, its <c>vault</c> schema reinstalled at every run by the
/// installation of the module (<see cref="DatabaseLifecycle.InstallAsync"/>) under the connection of the variable,
/// which owns it; the tests then connect under an ordinary role, <c>stellaegis_secrets_app</c>, created once, as
/// an application does. One fixture for the collection: the classes share the vault, and reset it in turn.
/// </summary>
/// <remarks>
/// The connection string comes from <c>STELLAEGIS_SECRETS_TEST_POSTGRES</c>, defaulting to a local
/// server and the database <c>stellaegis_secrets_tests</c>, created if missing. When no server answers,
/// <see cref="Unavailable"/> says why, and the tests that need it are skipped rather than failed.
/// </remarks>
[CollectionDefinition(PostgreSqlCollection.Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    /// <summary>The classes of the collection share one installed vault, and never run in parallel with one another.</summary>
    public const string Name = "PostgreSQL";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string Variable = "STELLAEGIS_SECRETS_TEST_POSTGRES";
    private const string Default  = "Host=127.0.0.1;Username=postgres;Database=stellaegis_secrets_tests";
    private const string AppRole  = "stellaegis_secrets_app";

    /// <summary>The connection string of the installer, which owns the schema.</summary>
    public string AdminConnectionString { get; } = Environment.GetEnvironmentVariable(Variable) ?? Default;

    /// <summary>The connection string the tests use, under the application's role.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Why the database cannot be used, or <c>null</c> when it can.</summary>
    public string? Unavailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var target = new NpgsqlConnectionStringBuilder(AdminConnectionString);
        var server = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = "postgres" };
        ConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = AppRole, Password = AppRole }.ConnectionString;

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

            await using (var connection = new NpgsqlConnection(AdminConnectionString))
            {
                await connection.OpenAsync();
                await using var reset = new NpgsqlCommand($"""
                    drop schema if exists {SecretsDatabase.Schema} cascade;
                    do $$ begin
                      if not exists (select 1 from pg_roles where rolname = '{AppRole}') then
                        create role {AppRole} login password '{AppRole}';
                      end if;
                    end $$;
                    """, connection);
                await reset.ExecuteNonQueryAsync();
            }

            await DatabaseLifecycle.InstallAsync(
                [SecretsDatabase.Value], ApplicationFrame.ManyCompartments(), OpenAdminAsync, AppRole, Actor.Job("tests"));
        }
        catch (NpgsqlException e)
        {
            Unavailable = $"No PostgreSQL for the vault tests ({Variable} or '{Default}'): {e.Message}";
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<System.Data.Common.DbConnection> OpenAdminAsync(ModuleDatabase _, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
