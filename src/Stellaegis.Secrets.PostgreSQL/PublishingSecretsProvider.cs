using Stellaegis.Backplane;
using Stellaegis.Backplane.SecretEvents;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Decorates an <see cref="IMutableSecretsProvider"/> to publish a <see cref="SecretChangedEvent"/> on the
/// backplane after every successful write, so subscribers can evict cached values resolved from that path.
/// </summary>
/// <remarks>
/// Reads pass straight through. The event is published after the write commits — if publication fails the
/// write still stands, and consumers heal on their cache TTL (the event is the fast path, the TTL the backstop).
/// </remarks>
internal sealed class PublishingSecretsProvider(IMutableSecretsProvider inner, IBackplane backplane) : IMutableSecretsProvider
{
    /// <inheritdoc/>
    public string Separator => inner.Separator;

    /// <inheritdoc/>
    public Task<string> GetSecretAsync(string path, CancellationToken ct = default) =>
        inner.GetSecretAsync(path, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(IEnumerable<string> paths, CancellationToken ct = default) =>
        inner.GetSecretsAsync(paths, ct);

    /// <inheritdoc/>
    public async Task SetSecretAsync(string path, string value, CancellationToken ct = default)
    {
        await inner.SetSecretAsync(path, value, ct).ConfigureAwait(false);
        await backplane.PublishAsync(new SecretChangedEvent { Path = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteSecretAsync(string path, CancellationToken ct = default)
    {
        await inner.DeleteSecretAsync(path, ct).ConfigureAwait(false);
        await backplane.PublishAsync(new SecretChangedEvent { Path = path }, ct).ConfigureAwait(false);
    }
}
