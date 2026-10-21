using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// Configuration for the vault client: the base address of the host mapping the secrets module and the timeout
/// of each call, validated when the host starts. Bind from the <c>Stellaegis:Secrets:Client</c> section.
/// </summary>
public sealed class VaultClientOptions : StellaegisClientOptions
{
    /// <summary>Default configuration section name: <c>Stellaegis:Secrets:Client</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets:Client";

    /// <summary>Creates the options; the timeout of a vault call defaults to ten seconds.</summary>
    public VaultClientOptions() => Timeout = TimeSpan.FromSeconds(10);
}
