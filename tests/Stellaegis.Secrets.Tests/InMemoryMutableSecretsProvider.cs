using Stellaegis.Primitives;

namespace Stellaegis.Secrets.Tests;

/// <summary>A writable in-memory store, for exercising the routes and the client: the two ends of the wire.</summary>
internal sealed class InMemoryMutableSecretsProvider : IMutableSecretsProvider, ISecretsPurge
{
    private readonly Dictionary<SecretPath, string> _secrets = [];

    public Actor? LastPurgedBy { get; private set; }

    public Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default) =>
        _secrets.TryGetValue(path, out var value)
            ? Task.FromResult(value)
            : throw new SecretNotFoundException(path);

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

    public Task<int> PurgeCompartmentAsync(Compartment compartment, Actor actor, CancellationToken cancellationToken = default)
    {
        LastPurgedBy = actor;
        var prefix = SecretPath.TextPrefixOf(compartment);
        var purged = _secrets.Keys.Where(p => p.ToString().StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var path in purged)
            _secrets.Remove(path);
        return Task.FromResult(purged.Count);
    }
}
