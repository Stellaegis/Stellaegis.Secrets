using Microsoft.Extensions.Logging;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets;

/// <summary>
/// In-memory implementation of <see cref="ISecretsProvider"/> intended for development and tests only.
/// </summary>
/// <remarks>
/// This provider stores secrets in memory and is not suitable for production. It keeps paths as they
/// are, with no native name to translate to.
/// </remarks>
public sealed class InMemorySecretsProvider : ISecretsProvider, ISecretsPurge
{
    private readonly Dictionary<SecretPath, string> _secrets = [];
    private readonly ILogger<InMemorySecretsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySecretsProvider"/> class.
    /// </summary>
    public InMemorySecretsProvider(ILogger<InMemorySecretsProvider> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using InMemorySecretsProvider (development/testing only).");
    }

    /// <summary>
    /// Adds or overwrites a secret in the in-memory store.
    /// </summary>
    public void AddSecret(SecretPath path, string value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        _secrets[path] = value;
        _logger.LogDebug("Added secret for path {Path}", path);
    }

    /// <inheritdoc />
    public Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (_secrets.TryGetValue(path, out var value))
        {
            _logger.LogDebug("Retrieved secret for path {Path}", path);
            return Task.FromResult(value);
        }

        _logger.LogError("Secret not found for path {Path}", path);
        throw new SecretNotFoundException(path);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new Dictionary<SecretPath, string>();

        foreach (var path in paths)
        {
            if (path is null) continue;

            if (_secrets.TryGetValue(path, out var value))
            {
                result[path] = value;
            }
            else
            {
                _logger.LogWarning("Secret not found during batch retrieval: {Path}", path);
            }
        }

        return Task.FromResult((IReadOnlyDictionary<SecretPath, string>)result);
    }

    /// <inheritdoc />
    public Task<int> PurgeCompartmentAsync(Compartment compartment, Actor actor, CancellationToken cancellationToken = default)
    {
        var prefix = SecretPath.TextPrefixOf(compartment);
        Guard.NotNull(actor);

        _logger.LogInformation("Purging the secrets of compartment {Compartment}, by {Actor}.", compartment, actor);

        var purged = _secrets.Keys.Where(p => p.ToString().StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var path in purged)
            _secrets.Remove(path);

        _logger.LogInformation("Purged {SecretCount} secrets of compartment {Compartment}, by {Actor}.", purged.Count, compartment, actor);
        return Task.FromResult(purged.Count);
    }
}
