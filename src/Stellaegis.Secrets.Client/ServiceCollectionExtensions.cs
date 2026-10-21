using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// Dependency injection helpers for Stellaegis.Secrets.Client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISecretsProvider"/>, <see cref="IMutableSecretsProvider"/> and <see cref="ISecretsPurge"/>
    /// backed by an HTTP client calling a host that maps the secrets module, its options bound from
    /// <paramref name="sectionName"/> and validated at host start, and the secrets module declared to the
    /// application.
    /// </summary>
    /// <remarks>
    /// The client sends requests as they are: authenticating them is the caller's concern. Add a
    /// <see cref="System.Net.Http.DelegatingHandler"/> to the returned builder to attach a token, an
    /// API key, or anything else the host requires. The typed client is built by the HTTP client factory, per
    /// resolution, as every typed client is.
    /// </remarks>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// "Stellaegis": {
    ///   "Secrets": {
    ///     "Client": { "BaseUrl": "https://vault.example.com/api/vault/" }
    ///   }
    /// }
    /// </code>
    /// Program.cs:
    /// <code>
    /// builder.Services.AddStellaegisHttpVaultClient(builder.Configuration)
    ///     .AddHttpMessageHandler&lt;MyTokenHandler&gt;();
    /// </code>
    /// </example>
    /// <returns>The builder of the underlying HTTP client, to add handlers to.</returns>
    public static IHttpClientBuilder AddStellaegisHttpVaultClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = VaultClientOptions.SectionName)
    {
        services.AddStellaegisSecrets();
        var builder = services.AddStellaegisHttpClient<IMutableSecretsProvider, HttpSecretsProvider, VaultClientOptions>(configuration, sectionName);
        services.AddTransient<ISecretsProvider>(sp => sp.GetRequiredService<IMutableSecretsProvider>());
        services.AddTransient<ISecretsPurge>(sp => (ISecretsPurge)sp.GetRequiredService<IMutableSecretsProvider>());
        return builder;
    }
}
