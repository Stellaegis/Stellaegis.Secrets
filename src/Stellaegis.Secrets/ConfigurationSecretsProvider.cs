using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Stellaegis.Secrets;

/// <summary>
/// Reads secrets from <see cref="IConfiguration"/> (User Secrets, appsettings, environment variables).
/// Intended for local development and self-hosted deployments without an external vault.
/// </summary>
/// <remarks>
/// Secret keys are stored under the "TenantSecrets" section using dotted paths:
/// <code>
/// "TenantSecrets": {
///   "tenantDB.postgresql.dev": "{\"connectionString\":\"Host=localhost;...\"}"
/// }
/// </code>
/// Use User Secrets for local dev to avoid committing connection strings.
/// </remarks>
public sealed class ConfigurationSecretsProvider : ISecretsProvider
{
    private const string SectionName = "TenantSecrets";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationSecretsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationSecretsProvider"/>.
    /// </summary>
    public ConfigurationSecretsProvider(IConfiguration configuration, ILogger<ConfigurationSecretsProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses "." as separator — the internal dotted path (e.g. "tenantDB.postgresql.dev") is used as-is
    /// as the configuration key, without any conversion.
    /// </remarks>
    public string Separator => ".";

    /// <inheritdoc />
    public Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var value = _configuration[$"{SectionName}:{path}"];
        if (value is null)
        {
            _logger.LogError("Secret not found in configuration for path '{Path}' (section '{Section}')", path, SectionName);
            throw new KeyNotFoundException($"Secret not found: {path}");
        }

        _logger.LogDebug("Retrieved secret from configuration for path '{Path}'", path);
        return Task.FromResult(value);
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
            if (string.IsNullOrWhiteSpace(path)) continue;

            var value = _configuration[$"{SectionName}:{path}"];
            if (value is not null)
                result[path] = value;
            else
                _logger.LogWarning("Secret not found in configuration during batch retrieval: '{Path}'", path);
        }

        return Task.FromResult((IReadOnlyDictionary<string, string>)result);
    }
}
