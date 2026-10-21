using System.Net.Http.Json;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// HTTP adapter for the vault API — implements <see cref="IMutableSecretsProvider"/> and <see cref="ISecretsPurge"/>
/// by calling a host that maps the secrets module. Used when the vault is not hosted in-process.
/// </summary>
/// <remarks>
/// Paths travel in their text form; the provider behind the host translates them into its own native names. The
/// managed exceptions of the module are raised again from the code the host answered: a missing secret is a
/// <see cref="SecretNotFoundException"/> here as in process. Any other failure is an <see cref="HttpRequestException"/>
/// carrying the status and the code, when the host sent one.
/// </remarks>
internal sealed class HttpSecretsProvider(HttpClient http) : IMutableSecretsProvider, ISecretsPurge
{
    /// <inheritdoc />
    public async Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(path);

        using var response = await http.PostAsJsonAsync("secrets/read", new ReadSecretRequest(path), cancellationToken);
        await ThrowOnErrorAsync(response, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(paths);

        using var response = await http.PostAsJsonAsync(
            "secrets/get",
            new GetSecretsRequest([.. paths.Where(p => p is not null)]),
            cancellationToken);
        await ThrowOnErrorAsync(response, cancellationToken);

        var secrets = await response.Content.ReadFromJsonAsync<Dictionary<SecretPath, string>>(cancellationToken: cancellationToken);
        return secrets ?? [];
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(SecretPath path, string value, CancellationToken ct = default)
    {
        Guard.NotNull(path);
        Guard.NotWhiteSpace(value);

        using var response = await http.PostAsJsonAsync("secrets/set", new SetSecretRequest(path, value), ct);
        await ThrowOnErrorAsync(response, ct);
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(SecretPath path, CancellationToken ct = default)
    {
        Guard.NotNull(path);

        using var response = await http.PostAsJsonAsync("secrets/delete", new DeleteSecretRequest(path), ct);
        await ThrowOnErrorAsync(response, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The host records the actor it authenticated for the call; <paramref name="actor"/> names the caller on this
    /// side only, in the request's trace.
    /// </remarks>
    public async Task<int> PurgeCompartmentAsync(Compartment compartment, Actor actor, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(compartment);
        Guard.NotNull(actor);

        using var response = await http.DeleteAsync($"secrets/compartments/{compartment.Value}", cancellationToken);
        await ThrowOnErrorAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
    }

    // The managed exception of the module comes back as it is raised in process; the rest is a transport failure.
    private static async Task ThrowOnErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (await response.ReadErrorAsync(cancellationToken) is not { } error)
            return;
        if (error.Code == SecretNotFoundException.Code)
            throw new SecretNotFoundException(error);
        throw new HttpRequestException(
            error.Code.Length > 0 ? $"The vault answered {error.Code}: {error.Message}" : $"The vault answered {(int)response.StatusCode}: {error.Message}",
            null, response.StatusCode);
    }
}
