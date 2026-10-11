using System.Net.Http.Json;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// HTTP adapter for the Vault API — implements <see cref="IMutableSecretsProvider"/> by
/// proxying calls to a remote host that exposes the vault over HTTP. Used when the vault is not
/// hosted in-process.
/// </summary>
/// <remarks>
/// Paths travel in their text form; the provider behind the remote host translates them into its
/// own native names.
/// </remarks>
internal sealed class HttpSecretsProvider(HttpClient http) : IMutableSecretsProvider
{
    /// <inheritdoc />
    public async Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var secrets = await GetSecretsAsync([path], cancellationToken);

        if (!secrets.TryGetValue(path, out var value))
            throw new KeyNotFoundException($"Secret not found: {path}");

        return value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var response = await http.PostAsJsonAsync(
            "secrets/get",
            new GetSecretsRequest([.. paths.Where(p => p is not null)]),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var secrets = await response.Content.ReadFromJsonAsync<Dictionary<SecretPath, string>>(cancellationToken: cancellationToken);
        return secrets ?? [];
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(SecretPath path, string value, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("secrets/set", new SetSecretRequest(path, value), ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(SecretPath path, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("secrets/delete", new DeleteSecretRequest(path), ct);
        response.EnsureSuccessStatusCode();
    }
}
