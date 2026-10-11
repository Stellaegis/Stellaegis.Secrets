using Microsoft.Extensions.DependencyInjection;

namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Dependency injection helpers for the Scaleway secrets provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ScalewaySecretsProvider"/> as the <see cref="ISecretsProvider"/> implementation.
    /// Uses Scaleway Secret Manager REST API.
    /// </summary>
    public static IServiceCollection AddScalewaySecretsProvider(
        this IServiceCollection services,
        Action<ScalewaySecretsOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient(ScalewaySecretsProvider.HttpClientName);
        services.AddSingleton<ISecretsProvider, ScalewaySecretsProvider>();
        return services;
    }
}
