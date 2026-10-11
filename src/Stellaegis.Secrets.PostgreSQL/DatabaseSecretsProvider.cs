using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Reads secrets from a PostgreSQL table encrypted with pgcrypto (pgp_sym_encrypt/decrypt).
/// Supports a modulo-2 key rotation pattern: two passphrases (slots 1 and 2) alternate
/// as the active slot — <see cref="DatabaseSecretsOptions.ActiveKeySlot"/> controls which
/// slot is used for new writes; <see cref="DatabaseSecretsOptions.Keys"/> provides both.
/// </summary>
public sealed class DatabaseSecretsProvider : IMutableSecretsProvider
{
    private const string Table = "vault.secrets";

    private readonly string _connectionString;
    private readonly DatabaseSecretsOptions _opts;
    private readonly ILogger<DatabaseSecretsProvider> _logger;

    /// <summary>Creates the provider, validating that the configured active key slot has a passphrase.</summary>
    public DatabaseSecretsProvider(IOptions<DatabaseSecretsOptions> options, ILogger<DatabaseSecretsProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _opts   = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_opts.ConnectionString))
            throw new ArgumentException("ConnectionString is required.", nameof(options));

        // Validate that the active slot has a passphrase (throws if not configured)
        _ = _opts.ActivePassphrase;

        _connectionString = _opts.ConnectionString;
    }

    /// <inheritdoc />
    public string Separator => ".";

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var result = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                $"""
                SELECT CASE key_slot
                    WHEN 1 THEN pgp_sym_decrypt(secret_json, @pass1)
                    WHEN 2 THEN pgp_sym_decrypt(secret_json, @pass2)
                END::text
                FROM {Table}
                WHERE path = @path
                """,
                new { path, pass1 = _opts.GetPassphrase(1), pass2 = _opts.GetPassphrase(2) },
                cancellationToken: cancellationToken));

        if (result is null)
        {
            _logger.LogError("Secret not found in database for path '{Path}'", path);
            throw new KeyNotFoundException($"Secret not found: {path}");
        }

        _logger.LogDebug("Retrieved secret from database for path '{Path}'", path);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var pathList = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (pathList.Length == 0)
            return new Dictionary<string, string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<(string path, string secret)>(
            new CommandDefinition(
                $"""
                SELECT path, CASE key_slot
                    WHEN 1 THEN pgp_sym_decrypt(secret_json, @pass1)
                    WHEN 2 THEN pgp_sym_decrypt(secret_json, @pass2)
                END::text AS secret
                FROM {Table}
                WHERE path = ANY(@paths)
                """,
                new { paths = pathList, pass1 = _opts.GetPassphrase(1), pass2 = _opts.GetPassphrase(2) },
                cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.path, r => r.secret, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string path, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var slot = _opts.ActiveKeySlot;
        var passphrase = _opts.ActivePassphrase;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO {Table} (path, secret_json, key_slot, created_at, updated_at)
            VALUES (@path, pgp_sym_encrypt(@value, @passphrase), @slot, now(), now())
            ON CONFLICT (path) DO UPDATE
              SET secret_json = pgp_sym_encrypt(@value, @passphrase),
                  key_slot    = @slot,
                  updated_at  = now()
            """,
            new { path, value, passphrase, slot },
            cancellationToken: ct));

        _logger.LogDebug("Set secret in database for path '{Path}' (slot {Slot})", path, slot);
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {Table} WHERE path = @path",
            new { path },
            cancellationToken: ct));

        _logger.LogDebug("Deleted secret from database for path '{Path}'", path);
    }
}
