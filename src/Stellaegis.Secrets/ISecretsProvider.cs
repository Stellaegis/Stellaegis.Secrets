namespace Stellaegis.Secrets;

/// <summary>
/// Retrieves secrets from a vault (e.g. Scaleway Secret Manager, HashiCorp Vault, Azure Key Vault).
/// </summary>
/// <remarks>
/// Secrets are identified by a <see cref="SecretPath"/>; each implementation translates it into its
/// vault's native name. Implementations must never log secret values.
/// </remarks>
public interface ISecretsProvider
{
    /// <summary>
    /// Retrieves the value of the secret at <paramref name="path"/>.
    /// </summary>
    /// <exception cref="SecretNotFoundException">No secret exists at <paramref name="path"/>.</exception>
    Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the values of the secrets at <paramref name="paths"/>. A path with no secret is
    /// absent from the result rather than an error.
    /// </summary>
    Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default);
}
