using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stellaegis.Primitives;
using Stellaegis.Primitives.AspNetCore;
using Stellaegis.Primitives.Hosting;
using Stellaegis.Secrets.AspNetCore;
using Stellaegis.Secrets.Client;

namespace Stellaegis.Secrets.Tests;

/// <summary>
/// The HTTP client against the mapped routes, in one in-memory host: the two ends of the wire. The host declares
/// a frame with compartments and a request context, which the routes of the module need for the actor.
/// </summary>
public sealed class VaultApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(WebApplication App, HttpSecretsProviderHandle Client, InMemoryMutableSecretsProvider Store)> StartAsync(
        Action<WebApplication> map, bool authenticated = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var store = new InMemoryMutableSecretsProvider();
        builder.Services.AddStellaegisFrame(ApplicationFrame.ManyCompartments().WithSharedSpace());
        builder.Services.AddStellaegisRequestContext(c => c.CompartmentFromRoute("org"));
        builder.Services.AddStellaegisSecrets();
        builder.Services.AddSingleton<ISecretsProvider>(store);
        builder.Services.AddSingleton<ISecretsPurge>(store);
        builder.Services.AddAuthentication(HeaderAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(HeaderAuthentication.SchemeName, null);
        builder.Services.AddAuthorization(o => o.AddPolicy("vault", p => p.RequireAuthenticatedUser()));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        map(app);
        await app.StartAsync(Ct);

        var services = new ServiceCollection();
        services.AddStellaegisHttpVaultClient(new ConfigurationBuilder()
                .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Stellaegis:Secrets:Client:BaseUrl", "http://localhost/api/vault/")])
                .Build())
            .ConfigurePrimaryHttpMessageHandler(() => app.GetTestServer().CreateHandler())
            .ConfigureHttpClient(http =>
            {
                if (authenticated)
                    http.DefaultRequestHeaders.Add(HeaderAuthentication.Header, "jane");
            });
        var provider = services.BuildServiceProvider();

        return (app, new HttpSecretsProviderHandle(provider.GetRequiredService<IMutableSecretsProvider>(), provider.GetRequiredService<ISecretsPurge>()), store);
    }

    internal sealed record HttpSecretsProviderHandle(IMutableSecretsProvider Secrets, ISecretsPurge Purge);

    [Fact]
    public async Task Open_routes_carry_writes_and_reads_through_and_a_missing_secret_comes_back_as_its_exception()
    {
        var (app, client, store) = await StartAsync(a => a.MapGroup("/api/vault").MapSecretsEndpoints().AllowAnonymous());
        await using var _ = app;
        var path = SecretPath.Parse("acme.billing.primary.postgresql");

        await client.Secrets.SetSecretAsync(path, "s3cret", Ct);
        Assert.Equal("s3cret", await store.GetSecretAsync(path, Ct));
        Assert.Equal("s3cret", await client.Secrets.GetSecretAsync(path, Ct));

        var batch = await client.Secrets.GetSecretsAsync([path, SecretPath.Parse("acme.billing.missing")], Ct);
        Assert.Equal(new Dictionary<SecretPath, string> { [path] = "s3cret" }, batch);

        await client.Secrets.DeleteSecretAsync(path, Ct);
        var missing = await Assert.ThrowsAsync<SecretNotFoundException>(() => client.Secrets.GetSecretAsync(path, Ct));
        Assert.Equal(SecretNotFoundException.Code, missing.Error.Code);
        Assert.Contains(path.ToString(), missing.Message);
    }

    [Fact]
    public async Task The_administration_route_purges_a_compartment_under_the_actor_the_host_authenticated()
    {
        var (app, client, store) = await StartAsync(
            a => a.MapGroup("/api/vault").MapSecretsAdminEndpoints().RequireAuthorization("vault"), authenticated: true);
        await using var _ = app;
        var acme = Compartment.Parse("acme");
        await store.SetSecretAsync(SecretPath.For<BillingModule>(acme, "a"), "v", Ct);
        await store.SetSecretAsync(SecretPath.For<BillingModule>(Space.Shared, "a"), "v", Ct);

        Assert.Equal(1, await client.Purge.PurgeCompartmentAsync(acme, Actor.Job("purge"), Ct));

        Assert.Equal(Actor.Parse("jane"), store.LastPurgedBy);
        Assert.Equal(0, await client.Purge.PurgeCompartmentAsync(acme, Actor.Job("purge"), Ct));
    }

    [Fact]
    public async Task Routes_behind_a_policy_refuse_an_unauthenticated_caller()
    {
        var (app, client, _) = await StartAsync(a => a.MapGroup("/api/vault").MapSecretsEndpoints().RequireAuthorization("vault"));
        await using var _ = app;

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.Secrets.GetSecretAsync(SecretPath.Parse("acme.billing.a"), Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task A_read_only_provider_answers_not_implemented_on_writes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddStellaegisFrame(ApplicationFrame.SingleCompartment("acme"));
        builder.Services.AddStellaegisRequestContext();
        builder.Services.AddStellaegisSecrets();
        builder.Services.AddSingleton<ISecretsProvider>(new InMemorySecretsProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemorySecretsProvider>.Instance));
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.MapGroup("/api/vault").MapSecretsEndpoints().AllowAnonymous();
        await app.StartAsync(Ct);

        var response = await app.GetTestClient().PostAsJsonAsync("/api/vault/secrets/set", new { path = "acme.billing.a", value = "v" }, Ct);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Routes_left_without_authorization_fail_the_startup()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartAsync(a => a.MapGroup("/api/vault").MapSecretsEndpoints()));

        Assert.Contains("secrets.read", exception.Message);
        Assert.Contains("declare no authorization", exception.Message);
    }

    [Fact]
    public async Task The_routes_act_on_spaces_and_are_refused_in_the_shared_space()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartAsync(a => a.MapGroup("/api/shared").MapSecretsEndpoints().InSharedSpace().AllowAnonymous()));

        Assert.Contains("act on spaces", exception.Message);
    }

    /// <summary>Authenticates a request carrying the header, as the actor it names.</summary>
    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Header";
        public const string Header = "X-Actor";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var actor))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", actor.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
