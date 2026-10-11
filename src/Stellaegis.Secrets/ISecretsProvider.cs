namespace Stellaegis.Secrets;

/// <summary>
/// Retrieves secrets from an external vault (e.g. Scaleway Secret Manager, HashiCorp Vault, Azure Key Vault).
/// </summary>
/// <remarks>
/// Implementations must never log secret values.
/// </remarks>
public interface ISecretsProvider
{
    /// <summary>
    /// Vault path separator (e.g. "/" for Scaleway, "-" for Azure Key Vault).
    /// Internal paths are typically dotted and may need conversion using this separator.
    /// </summary>
    string Separator { get; }

    /// <summary>
    /// Retrieves a secret value for the specified vault path.
    /// </summary>
    Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple secret values for the specified vault paths.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default);
}
