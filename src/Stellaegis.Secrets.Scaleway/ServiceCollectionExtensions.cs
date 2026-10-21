using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Dependency injection helpers for the Scaleway secrets provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ScalewaySecretsProvider"/> as the <see cref="ISecretsProvider"/> implementation,
    /// with its options bound from <paramref name="sectionName"/> and validated at host start, and the secrets
    /// module declared to the application. Uses Scaleway Secret Manager REST API.
    /// </summary>
    /// <remarks>
    /// To set an option from code, follow with
    /// <c>services.PostConfigure&lt;ScalewaySecretsOptions&gt;(o =&gt; ...)</c>.
    /// </remarks>
    public static IServiceCollection AddScalewaySecretsProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = ScalewaySecretsOptions.SectionName)
    {
        services.AddStellaegisOptions<ScalewaySecretsOptions>(configuration, sectionName);
        services.AddStellaegisSecrets();
        services.AddHttpClient(ScalewaySecretsProvider.HttpClientName);
        services.AddSingleton<ISecretsProvider, ScalewaySecretsProvider>();
        return services;
    }
}
