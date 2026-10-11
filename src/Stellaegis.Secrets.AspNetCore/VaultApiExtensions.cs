using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Stellaegis.AspNetCore;

namespace Stellaegis.Secrets.AspNetCore;

/// <summary>
/// Extension methods that configure the minimal Stellaegis Vault API host:
/// M2M authentication (scope <c>"vault"</c>), CORS, JSON serialization,
/// rate limiting, secret read/write endpoints, and a basic health check.
/// </summary>
/// <remarks>
/// Exposes <see cref="ISecretsProvider"/>/<see cref="IMutableSecretsProvider"/> over HTTP so that
/// other services (Admin.Registry, Trash, …) can reach the vault remotely instead of hosting it
/// themselves — "one service, one host". Write endpoints (<c>set</c>/<c>delete</c>) require an
/// <see cref="IMutableSecretsProvider"/> to be registered (e.g. <c>AddDatabaseSecretsProvider</c>);
/// they return <c>501 Not Implemented</c> when only a read-only <see cref="ISecretsProvider"/>
/// (e.g. <c>InMemorySecretsProvider</c>) is available.
/// </remarks>
/// <example>
/// <code>
/// var builder = WebApplication.CreateBuilder(args);
/// builder.Services.AddDatabaseSecretsProvider(opts => { ... });   // or AddInMemorySecretsProvider
/// builder.AddStellaegisVaultApi();
///
/// var app = builder.Build();
/// app.UseStellaegisVaultApi();
/// app.Run();
/// </code>
/// </example>
public static class VaultApiExtensions
{
    /// <summary>
    /// Registers all services required by the Vault API host:
    /// M2M authentication (scope <c>"vault"</c>), CORS, JSON serialization, and rate limiting.
    /// </summary>
    /// <remarks>
    /// Call <c>AddDatabaseSecretsProvider</c> (or another <see cref="ISecretsProvider"/>
    /// registration) <b>before</b> this method.
    /// </remarks>
    public static WebApplicationBuilder AddStellaegisVaultApi(this WebApplicationBuilder builder)
    {
        builder.AddStellaegisM2MAuthentication("vault");
        builder.Services.AddStellaegisCors(builder.Configuration, builder.Environment);
        builder.Services.AddStellaegisJsonOptions();
        builder.Services.AddStellaegisRateLimiter(builder.Configuration);
        return builder;
    }

    /// <summary>
    /// Configures the middleware pipeline and maps the Vault API endpoints:
    /// <list type="bullet">
    ///   <item><c>POST /api/vault/secrets/get</c>    — read one or more secrets</item>
    ///   <item><c>POST /api/vault/secrets/set</c>    — create or replace a secret</item>
    ///   <item><c>POST /api/vault/secrets/delete</c> — remove a secret</item>
    ///   <item><c>GET  /api/vault/health</c>          — health check (anonymous)</item>
    /// </list>
    /// </summary>
    public static WebApplication UseStellaegisVaultApi(this WebApplication app)
    {
        // ── Middleware pipeline ───────────────────────────────
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseStellaegisExceptionHandler();
        app.UseStellaegisSecurityHeaders();
        app.UseRateLimiter();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        // ── Endpoints ────────────────────────────────────────
        app.MapStellaegisHealth();
        app.MapStellaegisVaultEndpoints();

        return app;
    }

    /// <summary>
    /// Maps the Vault API endpoints without configuring the middleware pipeline.
    /// Use this when composing multiple APIs in a single host where the
    /// middleware is configured once (e.g. unified DevHost).
    /// </summary>
    public static WebApplication MapStellaegisVaultEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/vault/secrets")
            .RequireAuthorization(StellaegisM2MDefaults.PolicyName);

        group.MapPost("/get", async (
                ISecretsProvider provider,
                GetSecretsRequest req,
                CancellationToken ct) =>
            {
                var secrets = await provider.GetSecretsAsync(req.Paths, ct);
                return Results.Ok(secrets);
            })
            .WithName("GetVaultSecrets")
            .Produces<IReadOnlyDictionary<string, string>>(StatusCodes.Status200OK);

        group.MapPost("/set", async (
                ISecretsProvider provider,
                SetSecretRequest req,
                CancellationToken ct) =>
            {
                if (provider is not IMutableSecretsProvider mutable)
                    return Results.Problem("This vault is read-only.", statusCode: StatusCodes.Status501NotImplemented);

                await mutable.SetSecretAsync(req.Path, req.Value, ct);
                return Results.NoContent();
            })
            .WithName("SetVaultSecret")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/delete", async (
                ISecretsProvider provider,
                DeleteSecretRequest req,
                CancellationToken ct) =>
            {
                if (provider is not IMutableSecretsProvider mutable)
                    return Results.Problem("This vault is read-only.", statusCode: StatusCodes.Status501NotImplemented);

                await mutable.DeleteSecretAsync(req.Path, ct);
                return Results.NoContent();
            })
            .WithName("DeleteVaultSecret")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }
}
