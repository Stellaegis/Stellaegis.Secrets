namespace Stellaegis.Secrets;

/// <summary>
/// Extends <see cref="ISecretsProvider"/> with write operations.
/// Not all vault backends support writes — register only when provisioning is needed.
/// </summary>
public interface IMutableSecretsProvider : ISecretsProvider
{
    /// <summary>
    /// Creates or replaces the secret at <paramref name="path"/>.
    /// </summary>
    Task SetSecretAsync(SecretPath path, string value, CancellationToken ct = default);

    /// <summary>
    /// Removes the secret at <paramref name="path"/>. No-op if no secret exists there.
    /// </summary>
    Task DeleteSecretAsync(SecretPath path, CancellationToken ct = default);
}
