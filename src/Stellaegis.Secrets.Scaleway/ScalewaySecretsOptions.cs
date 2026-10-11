namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Options for <see cref="ScalewaySecretsProvider"/> — Scaleway Secret Manager vault.
/// </summary>
public sealed class ScalewaySecretsOptions
{
    /// <summary>
    /// Scaleway region (e.g. "fr-par", "nl-ams", "pl-waw").
    /// </summary>
    public string Region { get; set; } = "fr-par";

    /// <summary>
    /// Scaleway project ID (UUID).
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Scaleway API secret key (X-Auth-Token). Should be sourced from an environment variable.
    /// </summary>
    public string ApiToken { get; set; } = "";

    /// <summary>
    /// Base URL for the Scaleway API (default: production endpoint).
    /// Override for testing or private endpoints.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.scaleway.com";
}
