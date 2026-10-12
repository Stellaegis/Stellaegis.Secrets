using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Stellaegis.Secrets.AspNetCore;

/// <summary>
/// Maps the Vault API: <see cref="ISecretsProvider"/>/<see cref="IMutableSecretsProvider"/> exposed
/// over HTTP, for the client in <c>Stellaegis.Secrets.Client</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three endpoints, relative to the builder they are mapped on:
/// </para>
/// <list type="bullet">
///   <item><c>POST secrets/get</c>    — read one or more secrets</item>
///   <item><c>POST secrets/set</c>    — create or replace a secret</item>
///   <item><c>POST secrets/delete</c> — remove a secret</item>
/// </list>
/// <para>
/// The endpoints return secret values in clear, so access is never left implicit: either name the
/// authorization policy that protects them, or open them deliberately with
/// <see cref="MapSecretsEndpointsAnonymous"/>. Everything else about the host — authentication
/// schemes, CORS, rate limiting, forwarded headers, health checks — is the caller's to configure.
/// </para>
/// <para>
/// Write endpoints answer <c>501 Not Implemented</c> when the registered provider is read-only
/// (no <see cref="IMutableSecretsProvider"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddDatabaseSecretsProvider(builder.Configuration);
/// builder.Services.AddAuthentication(...);
/// builder.Services.AddAuthorization(o => o.AddPolicy("vault", p => ...));
///
/// var app = builder.Build();
/// app.UseAuthentication();
/// app.UseAuthorization();
/// app.MapGroup("/api/vault").MapSecretsEndpoints(authorizationPolicy: "vault");
/// </code>
/// </example>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Vault API endpoints, each call required to satisfy <paramref name="authorizationPolicy"/>.
    /// </summary>
    /// <param name="endpoints">The builder to map the endpoints on — the application, or a route group.</param>
    /// <param name="authorizationPolicy">Name of the authorization policy every call must satisfy.</param>
    /// <returns>The group holding the endpoints, for further conventions.</returns>
    public static RouteGroupBuilder MapSecretsEndpoints(this IEndpointRouteBuilder endpoints, string authorizationPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationPolicy);
        return MapEndpoints(endpoints).RequireAuthorization(authorizationPolicy);
    }

    /// <summary>
    /// Maps the Vault API endpoints with no authorization at all: anyone who can reach the host can
    /// read, write and delete every secret. Meant for hosts reachable only from a trusted network.
    /// </summary>
    /// <param name="endpoints">The builder to map the endpoints on — the application, or a route group.</param>
    /// <returns>The group holding the endpoints, for further conventions.</returns>
    public static RouteGroupBuilder MapSecretsEndpointsAnonymous(this IEndpointRouteBuilder endpoints) =>
        MapEndpoints(endpoints).AllowAnonymous();

    private static RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("secrets");

        group.MapPost("/get", async (
                ISecretsProvider provider,
                GetSecretsRequest req,
                CancellationToken ct) =>
            {
                var secrets = await provider.GetSecretsAsync(req.Paths, ct);
                return Results.Ok(secrets);
            })
            .WithName("GetVaultSecrets")
            .Produces<IReadOnlyDictionary<SecretPath, string>>(StatusCodes.Status200OK);

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

        return group;
    }
}
