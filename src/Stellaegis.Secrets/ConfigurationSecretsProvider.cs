using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Stellaegis.Secrets;

/// <summary>
/// Reads secrets from <see cref="IConfiguration"/> (User Secrets, appsettings, environment variables).
/// Intended for local development and self-hosted deployments without an external vault.
/// </summary>
/// <remarks>
/// Secrets are keys of one configuration section, named by <see cref="NameFormat"/> — the text form
/// of the path:
/// <code>
/// "Stellaegis": {
///   "Secrets": {
///     "Configuration": {
///       "tenantDB.primary.postgresql.acme": "{\"connectionString\":\"Host=localhost;...\"}"
///     }
///   }
/// }
/// </code>
/// Use User Secrets for local dev to avoid committing connection strings.
/// </remarks>
public sealed class ConfigurationSecretsProvider : ISecretsProvider
{
    /// <summary>Default configuration section holding the secrets: <c>Stellaegis:Secrets:Configuration</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets:Configuration";

    private readonly IConfigurationSection _section;
    private readonly ILogger<ConfigurationSecretsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationSecretsProvider"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="sectionName">The section holding the secrets. Defaults to <see cref="SectionName"/>.</param>
    public ConfigurationSecretsProvider(
        IConfiguration configuration,
        ILogger<ConfigurationSecretsProvider> logger,
        string sectionName = SectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        _section = configuration.GetSection(sectionName);
        _logger  = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The native names of this provider: the text form of the path, as a configuration key.</summary>
    public static ISecretNameFormat NameFormat => DottedSecretNameFormat.Instance;

    /// <inheritdoc />
    public Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var value = _section[NameFormat.Join(path)];
        if (value is null)
        {
            _logger.LogError("Secret not found in configuration for path '{Path}' (section '{Section}')", path, _section.Path);
            throw new KeyNotFoundException($"Secret not found: {path}");
        }

        _logger.LogDebug("Retrieved secret from configuration for path '{Path}'", path);
        return Task.FromResult(value);
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

            var value = _section[NameFormat.Join(path)];
            if (value is not null)
                result[path] = value;
            else
                _logger.LogWarning("Secret not found in configuration during batch retrieval: '{Path}'", path);
        }

        return Task.FromResult((IReadOnlyDictionary<SecretPath, string>)result);
    }
}
