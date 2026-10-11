using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Stellaegis.Secrets.Client;

/// <summary>
/// Dependency injection helpers for Stellaegis.Secrets.Client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISecretsProvider"/>/<see cref="IMutableSecretsProvider"/> backed by
    /// an HTTP client targeting the Vault API.
    /// </summary>
    /// <remarks>
    /// The client sends requests as they are: authenticating them is the caller's concern. Add a
    /// <see cref="System.Net.Http.DelegatingHandler"/> to the returned builder to attach a token, an
    /// API key, or anything else the Vault API host requires.
    /// </remarks>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// "Stellaegis": {
    ///   "ExternalServices": {
    ///     "Vault": { "BaseUrl": "https://vault.example.com/api/vault/" }
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
        services.Configure<VaultClientOptions>(configuration.GetSection(sectionName));

        var builder = services.AddHttpClient<IMutableSecretsProvider, HttpSecretsProvider>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<VaultClientOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            http.Timeout     = opts.Timeout;
        });

        services.AddTransient<ISecretsProvider>(sp => sp.GetRequiredService<IMutableSecretsProvider>());

        return builder;
    }
}
