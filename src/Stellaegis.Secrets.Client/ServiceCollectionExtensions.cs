using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stellaegis.Http;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// Dependency injection helpers for Stellaegis.Secrets.Client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISecretsProvider"/>/<see cref="IMutableSecretsProvider"/> backed by
    /// an HTTP client targeting the Vault API, authenticated via <see cref="M2MTokenProvider"/>.
    /// Requires <see cref="M2MTokenProvider"/> to be registered (call <c>AddStellaegisM2M()</c> first).
    /// </summary>
    /// <remarks>
    /// Use this in multi-host ("Full") topologies where the vault is hosted by a standalone
    /// <c>Stellaegis.Platform.Vault</c>. In Lite/DevHost-unified topologies, register
    /// <c>DatabaseSecretsProvider</c> (or <c>InMemorySecretsProvider</c>) directly instead —
    /// it already implements the same port in-process, no adapter needed.
    /// </remarks>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// "Stellaegis": {
    ///   "M2M": { "TokenEndpoint": "...", "ClientId": "...", "ClientSecret": "..." },
    ///   "ExternalServices": {
    ///     "Vault": { "BaseUrl": "https://vault.api.stellaegis.local/api/vault/" }
    ///   }
    /// }
    /// </code>
    /// Program.cs:
    /// <code>
    /// builder.Services.AddStellaegisM2M(builder.Configuration);
    /// builder.Services.AddStellaegisHttpVaultClient(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddStellaegisHttpVaultClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = VaultClientOptions.SectionName)
    {
        if (!services.Any(d => d.ServiceType == typeof(M2MTokenProvider)))
            throw new InvalidOperationException(
                "M2MTokenProvider is not registered. Call AddStellaegisM2M() before AddStellaegisHttpVaultClient().");

        services.Configure<VaultClientOptions>(configuration.GetSection(sectionName));

        services.AddHttpClient<IMutableSecretsProvider, HttpSecretsProvider>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<VaultClientOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            http.Timeout     = opts.Timeout;
        })
        .AddHttpMessageHandler(sp =>
        {
            var opts     = sp.GetRequiredService<IOptions<VaultClientOptions>>().Value;
            var provider = sp.GetRequiredService<M2MTokenProvider>();
            return new M2MTokenHandler(provider, opts.Scope);
        });

        services.AddTransient<ISecretsProvider>(sp => sp.GetRequiredService<IMutableSecretsProvider>());

        return services;
    }
}
