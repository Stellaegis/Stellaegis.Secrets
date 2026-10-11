using Microsoft.Extensions.Logging;

namespace Stellaegis.Secrets;

/// <summary>
/// In-memory implementation of <see cref="ISecretsProvider"/> intended for development and tests only.
/// </summary>
/// <remarks>
/// This provider stores secrets in memory and is not suitable for production.
/// </remarks>
public sealed class InMemorySecretsProvider : ISecretsProvider
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
    private readonly ILogger<InMemorySecretsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySecretsProvider"/> class.
    /// </summary>
    public InMemorySecretsProvider(ILogger<InMemorySecretsProvider> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using InMemorySecretsProvider (development/testing only).");
    }

    /// <inheritdoc />
    public string Separator => "/";

    /// <summary>
    /// Adds or overwrites a secret in the in-memory store.
    /// </summary>
    public void AddSecret(string path, string value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        _secrets[path] = value;
        _logger.LogDebug("Added secret for path {Path}", path);
    }

    /// <inheritdoc />
    public Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (_secrets.TryGetValue(path, out var value))
        {
            _logger.LogDebug("Retrieved secret for path {Path}", path);
            return Task.FromResult(value);
        }

        _logger.LogError("Secret not found for path {Path}", path);
        throw new KeyNotFoundException($"Secret not found: {path}");
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

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

        return Task.FromResult((IReadOnlyDictionary<string, string>)result);
    }
}
