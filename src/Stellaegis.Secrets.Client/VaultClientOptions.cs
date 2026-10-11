namespace Stellaegis.Secrets.Client;

/// <summary>
/// Configuration for the Vault API client.
/// Bind from the <c>Stellaegis:Secrets:Client</c> configuration section.
/// </summary>
public sealed class VaultClientOptions
{
    /// <summary>Default configuration section name: <c>Stellaegis:Secrets:Client</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets:Client";

    /// <summary>Base URL of the Vault API (e.g. <c>https://vault.example.com/api/vault/</c>).</summary>
    public required string BaseUrl { get; init; }

    /// <summary>HTTP timeout for vault requests. Defaults to 10 seconds.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
