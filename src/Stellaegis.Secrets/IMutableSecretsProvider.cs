namespace Stellaegis.Secrets;

/// <summary>
/// Extends <see cref="ISecretsProvider"/> with write operations.
/// Not all vault backends support writes — register only when provisioning is needed.
/// </summary>
public interface IMutableSecretsProvider : ISecretsProvider
{
    /// <summary>
    /// Creates or replaces a secret at the specified vault path.
    /// </summary>
    Task SetSecretAsync(string path, string value, CancellationToken ct = default);

    /// <summary>
    /// Removes a secret at the specified vault path. No-op if the path does not exist.
    /// </summary>
    Task DeleteSecretAsync(string path, CancellationToken ct = default);
}
