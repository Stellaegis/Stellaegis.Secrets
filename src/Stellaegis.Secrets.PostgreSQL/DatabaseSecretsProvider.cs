using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Reads secrets from a PostgreSQL table encrypted with pgcrypto (pgp_sym_encrypt/decrypt).
/// Supports a modulo-2 key rotation pattern: two passphrases (slots 1 and 2) alternate
/// as the active slot — <see cref="DatabaseSecretsOptions.ActiveKeySlot"/> controls which
/// slot is used for new writes; <see cref="DatabaseSecretsOptions.Keys"/> provides both.
/// </summary>
public sealed class DatabaseSecretsProvider : IMutableSecretsProvider, ISecretsPurge
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

    /// <summary>The native names of this provider: the text form of the path, in the <c>path</c> column.</summary>
    public static ISecretNameFormat NameFormat => DottedSecretNameFormat.Instance;

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

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
                new { path = NameFormat.Join(path), pass1 = _opts.GetPassphrase(1), pass2 = _opts.GetPassphrase(2) },
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
    public async Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // Native name → requested path: rows map back to what was asked, never parsed from the table.
        var requested = new Dictionary<string, SecretPath>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (path is not null)
                requested[NameFormat.Join(path)] = path;
        }

        if (requested.Count == 0)
            return new Dictionary<SecretPath, string>();

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
                new { paths = requested.Keys.ToArray(), pass1 = _opts.GetPassphrase(1), pass2 = _opts.GetPassphrase(2) },
                cancellationToken: cancellationToken));

        return rows.ToDictionary(r => requested[r.path], r => r.secret);
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(SecretPath path, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
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
            new { path = NameFormat.Join(path), value, passphrase, slot },
            cancellationToken: ct));

        _logger.LogDebug("Set secret in database for path '{Path}' (slot {Slot})", path, slot);
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(SecretPath path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {Table} WHERE path = @path",
            new { path = NameFormat.Join(path) },
            cancellationToken: ct));

        _logger.LogDebug("Deleted secret from database for path '{Path}'", path);
    }

    /// <inheritdoc />
    public async Task<int> PurgeCompartmentAsync(Compartment compartment, Actor actor, CancellationToken cancellationToken = default)
    {
        var prefix = SecretPath.TextPrefixOf(compartment);
        Guard.NotNull(actor);

        // Logged before anything is deleted: an interrupted purge still names its author.
        _logger.LogInformation("Purging the secrets of compartment {Compartment}, by {Actor}.", compartment, actor);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // The prefix holds no LIKE wildcard: a compartment is made of lowercase letters, digits and hyphens.
        var purged = await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {Table} WHERE path LIKE @pattern",
            new { pattern = prefix + "%" },
            cancellationToken: cancellationToken));

        _logger.LogInformation("Purged {SecretCount} secrets of compartment {Compartment}, by {Actor}.", purged, compartment, actor);
        return purged;
    }
}
