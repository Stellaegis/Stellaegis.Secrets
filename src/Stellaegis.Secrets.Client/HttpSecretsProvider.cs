using System.Net.Http.Json;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// HTTP adapter for the Vault API — implements <see cref="IMutableSecretsProvider"/> by
/// proxying calls to a remote host that exposes the vault over HTTP. Used when the vault is not
/// hosted in-process.
/// </summary>
internal sealed class HttpSecretsProvider(HttpClient http) : IMutableSecretsProvider
{
    /// <inheritdoc />
    public string Separator => ".";

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default)
    {
        var secrets = await GetSecretsAsync([path], cancellationToken);

        if (!secrets.TryGetValue(path, out var value))
            throw new KeyNotFoundException($"Secret not found: {path}");

        return value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            "secrets/get",
            new GetSecretsRequest([.. paths]),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var secrets = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cancellationToken);
        return secrets ?? new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string path, string value, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("secrets/set", new SetSecretRequest(path, value), ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(string path, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("secrets/delete", new DeleteSecretRequest(path), ct);
        response.EnsureSuccessStatusCode();
    }
}
