using Microsoft.Extensions.DependencyInjection;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Dependency injection helpers for the PostgreSQL secrets provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DatabaseSecretsProvider"/> as both <see cref="ISecretsProvider"/> and
    /// <see cref="IMutableSecretsProvider"/> (same singleton instance — it implements both).
    /// Uses a PostgreSQL table with pgcrypto encryption as a self-hosted vault.
    /// </summary>
    public static IServiceCollection AddDatabaseSecretsProvider(
        this IServiceCollection services,
        Action<DatabaseSecretsOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<DatabaseSecretsProvider>();
        services.AddSingleton<ISecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        services.AddSingleton<IMutableSecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        return services;
    }
}
