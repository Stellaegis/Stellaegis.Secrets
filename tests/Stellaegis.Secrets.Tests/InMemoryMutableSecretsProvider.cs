namespace Stellaegis.Secrets.Tests;

/// <summary>A writable in-memory store, for exercising the write endpoints.</summary>
internal sealed class InMemoryMutableSecretsProvider : IMutableSecretsProvider
{
    private readonly Dictionary<SecretPath, string> _secrets = [];

    public Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default) =>
        _secrets.TryGetValue(path, out var value)
            ? Task.FromResult(value)
            : throw new KeyNotFoundException($"Secret not found: {path}");

    public Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<SecretPath, string>>(
            paths.Where(_secrets.ContainsKey).ToDictionary(p => p, p => _secrets[p]));

    public Task SetSecretAsync(SecretPath path, string value, CancellationToken ct = default)
    {
        _secrets[path] = value;
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(SecretPath path, CancellationToken ct = default)
    {
        _secrets.Remove(path);
        return Task.CompletedTask;
    }
}
