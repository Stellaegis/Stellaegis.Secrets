using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stellaegis.Secrets.AspNetCore;
using Stellaegis.Secrets.Client;

namespace Stellaegis.Secrets.Tests;

/// <summary>
/// The HTTP client against the mapped endpoints, in one in-memory host: the two ends of the wire.
/// </summary>
public sealed class VaultApiTests
{
    private static async Task<(WebApplication App, IMutableSecretsProvider Client, InMemoryMutableSecretsProvider Store)> StartAsync(
        Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var store = new InMemoryMutableSecretsProvider();
        builder.Services.AddSingleton<ISecretsProvider>(store);
        builder.Services.AddAuthentication(NeverAuthenticated.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, NeverAuthenticated>(NeverAuthenticated.SchemeName, null);
        builder.Services.AddAuthorization(o => o.AddPolicy("vault", p => p.RequireAuthenticatedUser()));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddStellaegisHttpVaultClient(new ConfigurationBuilder()
                .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Stellaegis:Secrets:Client:BaseUrl", "http://localhost/api/vault/")])
                .Build())
            .ConfigurePrimaryHttpMessageHandler(() => app.GetTestServer().CreateHandler());
        var client = services.BuildServiceProvider().GetRequiredService<IMutableSecretsProvider>();

        return (app, client, store);
    }

    [Fact]
    public async Task Anonymous_endpoints_carry_writes_and_reads_through()
    {
        var (app, client, store) = await StartAsync(a => a.MapGroup("/api/vault").MapSecretsEndpointsAnonymous());
        await using var _ = app;
        var ct   = TestContext.Current.CancellationToken;
        var path = SecretPath.Parse("acme.billing.primary.postgresql");

        await client.SetSecretAsync(path, "s3cret", ct);
        Assert.Equal("s3cret", await store.GetSecretAsync(path, ct));
        Assert.Equal("s3cret", await client.GetSecretAsync(path, ct));

        var batch = await client.GetSecretsAsync([path, SecretPath.Parse("acme.billing.missing")], ct);
        Assert.Equal(new Dictionary<SecretPath, string> { [path] = "s3cret" }, batch);

        await client.DeleteSecretAsync(path, ct);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => client.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task Endpoints_behind_a_policy_refuse_an_unauthenticated_caller()
    {
        var (app, client, _) = await StartAsync(a => a.MapGroup("/api/vault").MapSecretsEndpoints(authorizationPolicy: "vault"));
        await using var _ = app;

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetSecretAsync(SecretPath.Parse("acme.billing.a"), TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public void A_policy_name_is_required()
    {
        var app = WebApplication.CreateBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapSecretsEndpoints(authorizationPolicy: " "));
    }

    private sealed class NeverAuthenticated(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Never";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
