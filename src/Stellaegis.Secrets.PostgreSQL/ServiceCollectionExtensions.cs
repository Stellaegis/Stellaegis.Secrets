using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Stellaegis.Primitives.Data;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Dependency injection helpers for the PostgreSQL secrets provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DatabaseSecretsProvider"/> as <see cref="ISecretsProvider"/>,
    /// <see cref="IMutableSecretsProvider"/> and <see cref="ISecretsPurge"/> (same singleton instance — it
    /// implements all three), with its options bound from <paramref name="sectionName"/> and validated at host
    /// start, the secrets module declared to the application, and the vault's database declared for its
    /// verification at startup (<see cref="SecretsDatabase"/>).
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
        services.AddStellaegisOptions<DatabaseSecretsOptions>(configuration, sectionName);
        services.AddStellaegisSecrets();
        services.AddStellaegisModuleDatabase(SecretsDatabase.Value, OpenAsApplicationAsync);
        services.AddSingleton<DatabaseSecretsProvider>();
        services.AddSingleton<ISecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        services.AddSingleton<IMutableSecretsProvider>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        services.AddSingleton<ISecretsPurge>(sp => sp.GetRequiredService<DatabaseSecretsProvider>());
        return services;
    }

    // The verification opens the vault as the provider does: under the connection string of the options.
    private static async Task<DbConnection> OpenAsApplicationAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(services.GetRequiredService<IOptions<DatabaseSecretsOptions>>().Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
