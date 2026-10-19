using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Dependency injection helpers for the PostgreSQL secrets provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DatabaseSecretsProvider"/> as <see cref="ISecretsProvider"/>,
    /// <see cref="IMutableSecretsProvider"/> and <see cref="ISecretsPurge"/> (same singleton instance — it
    /// implements all three), with its options bound from <paramref name="sectionName"/>. The options are
    /// validated at host start; without a host, on first resolution.
    /// Uses a PostgreSQL table with pgcrypto encryption as a self-hosted vault.
    /// </summary>
    /// <remarks>
    /// To set an option from code — a connection string read elsewhere, for instance — follow with
    /// <c>services.PostConfigure&lt;DatabaseSecretsOptions&gt;(o =&gt; ...)</c>.
    /// </remarks>
    public static IServiceCollection AddDatabaseSecretsProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DatabaseSecretsOptions.SectionName)
    {
        services.AddOptions<DatabaseSecretsOptions>().Bind(configuration.GetSection(sectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseSecretsOptions>, DatabaseSecretsOptionsValidator>());
        services.AddSingleton<DatabaseSecretsProvider>();
        services.AddSingleton<ISecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        services.AddSingleton<IMutableSecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        services.AddSingleton<ISecretsPurge>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        return services;
    }
}
