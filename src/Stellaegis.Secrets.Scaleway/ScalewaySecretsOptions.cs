using Microsoft.Extensions.Options;

namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Options for <see cref="ScalewaySecretsProvider"/> — Scaleway Secret Manager vault.
/// </summary>
public sealed class ScalewaySecretsOptions
{
    /// <summary>Default configuration section: <c>Stellaegis:Secrets:Scaleway</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets:Scaleway";

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

    /// <summary>The first setting that keeps the provider from working, or <see langword="null"/>.</summary>
    internal string? FindError()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            return "ProjectId is required.";

        if (string.IsNullOrWhiteSpace(ApiToken))
            return "ApiToken is required.";

        return null;
    }
}

/// <summary>
/// Validates <see cref="ScalewaySecretsOptions"/> when they are built — at host start, with <c>ValidateOnStart</c>.
/// </summary>
internal sealed class ScalewaySecretsOptionsValidator : IValidateOptions<ScalewaySecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, ScalewaySecretsOptions options) =>
        options.FindError() is { } error ? ValidateOptionsResult.Fail(error) : ValidateOptionsResult.Success;
}
