namespace Stellaegis.Secrets.Client;

/// <summary>
/// Configuration for the Vault API client.
/// Bind from the <c>Stellaegis:ExternalServices:Vault</c> configuration section.
/// </summary>
public sealed class VaultClientOptions
{
    /// <summary>Default configuration section name: <c>Stellaegis:ExternalServices:Vault</c>.</summary>
    public const string SectionName = "Stellaegis:ExternalServices:Vault";

    /// <summary>Base URL of the Vault API (e.g. <c>https://vault.api.stellaegis.local/api/vault/</c>).</summary>
    public required string BaseUrl { get; init; }

    /// <summary>OAuth2 scope required to call the Vault API. Defaults to <c>vault</c>.</summary>
    public string Scope { get; init; } = "vault";

    /// <summary>HTTP timeout for vault requests. Defaults to 10 seconds.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
