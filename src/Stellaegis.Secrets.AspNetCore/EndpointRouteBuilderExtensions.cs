using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stellaegis.Primitives;
using Stellaegis.Primitives.AspNetCore;

namespace Stellaegis.Secrets.AspNetCore;

/// <summary>
/// Maps the routes of the secrets module: <see cref="ISecretsProvider"/> and <see cref="IMutableSecretsProvider"/>
/// exposed over HTTP, for the client in <c>Stellaegis.Secrets.Client</c>; and, apart, its administration route,
/// the purge of a compartment.
/// </summary>
/// <remarks>
/// <para>
/// The routes take the path of a secret as their object: they act <b>on</b> spaces rather than in one
/// (<c>ActsOnSpaces</c>), so the host needs a request context — a frame with compartments and
/// <c>AddStellaegisRequestContext</c> — for the actor, and never reads a space from the request. Each route is
/// labelled with an <see cref="Operation"/> of <see cref="SecretOperations"/>, and the startup check of
/// <c>Stellaegis.Primitives.AspNetCore</c> refuses a route the host left without authorization: the routes return
/// secret values in clear, and the host declares who may call them, by operation or on the group, or opens them
/// deliberately with <c>AllowAnonymous()</c> on a host reachable only from a trusted network.
/// </para>
/// <list type="bullet">
///   <item><c>POST secrets/read</c>   — read one secret, <c>secrets.read</c>; 404 with the code <c>Secret.NotFound</c></item>
///   <item><c>POST secrets/get</c>    — read several secrets, <c>secrets.read</c>; a missing one is absent</item>
///   <item><c>POST secrets/set</c>    — create or replace a secret, <c>secrets.write</c></item>
///   <item><c>POST secrets/delete</c> — remove a secret, <c>secrets.delete</c></item>
///   <item><c>DELETE secrets/compartments/{compartment}</c> — purge a compartment, <c>secrets.purge</c>, administration</item>
/// </list>
/// <para>
/// Write routes answer <c>501 Not Implemented</c> when the registered provider is read-only; the purge answers it
/// when no <see cref="ISecretsPurge"/> is registered. The routes carry no name, so the vault can be mapped several
/// times in one host.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddStellaegisFrame(ApplicationFrame.ManyCompartments().WithSharedSpace());
/// builder.Services.AddStellaegisRequestContext(c => c.CompartmentFromRoute("org"));
/// builder.Services.AddDatabaseSecretsProvider(builder.Configuration);
/// builder.Services.AddAuthorization(o => o.AddPolicy("vault", p => p.RequireAuthenticatedUser()));
///
/// var app = builder.Build();
/// app.UseAuthentication();
/// app.UseAuthorization();
/// app.MapGroup("/api/vault").MapSecretsEndpoints().RequireAuthorization("vault");
/// app.MapGroup("/api/admin/vault").MapSecretsAdminEndpoints().RequireAuthorization("platform-admin");
/// </code>
/// </example>
public static class EndpointRouteBuilderExtensions
{
    private const string ReadOnly = "This vault is read-only.";

    /// <summary>
    /// Maps the routes of the secrets module, relative to <paramref name="endpoints"/>: reading, writing and deleting
    /// a secret. The host declares who may call them on the returned group.
    /// </summary>
    /// <param name="endpoints">The builder to map the routes on — the application, or a route group.</param>
    /// <returns>The group holding the routes, for the host's authorization and further conventions.</returns>
    public static RouteGroupBuilder MapSecretsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = Group(endpoints);

        group.MapPost("/read", async (ISecretsProvider provider, ReadSecretRequest request, CancellationToken ct) =>
            {
                if (request?.Path is not { } path)
                    return InvalidPath();
                return Results.Text(await provider.GetSecretAsync(path, ct), "text/plain");
            })
            .WithMetadata(SecretOperations.Read).LoadsNoResource()
            .Produces<string>(StatusCodes.Status200OK, "text/plain")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/get", async (ISecretsProvider provider, GetSecretsRequest request, CancellationToken ct) =>
            {
                if (request?.Paths is not { } paths)
                    return InvalidPath();
                return Results.Ok(await provider.GetSecretsAsync(paths, ct));
            })
            .WithMetadata(SecretOperations.Read).LoadsNoResource()
            .Produces<IReadOnlyDictionary<SecretPath, string>>(StatusCodes.Status200OK);

        group.MapPost("/set", async (ISecretsProvider provider, SetSecretRequest request, CancellationToken ct) =>
            {
                if (request?.Path is not { } path)
                    return InvalidPath();
                if (string.IsNullOrWhiteSpace(request.Value))
                    return Result.Failure(Error.Validation("Secret.ValueRequired", "A secret has a value.")).ToHttpResult();
                if (provider is not IMutableSecretsProvider mutable)
                    return Results.Problem(ReadOnly, statusCode: StatusCodes.Status501NotImplemented);

                await mutable.SetSecretAsync(path, request.Value, ct);
                return Results.NoContent();
            })
            .WithMetadata(SecretOperations.Write).LoadsNoResource()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/delete", async (ISecretsProvider provider, DeleteSecretRequest request, CancellationToken ct) =>
            {
                if (request?.Path is not { } path)
                    return InvalidPath();
                if (provider is not IMutableSecretsProvider mutable)
                    return Results.Problem(ReadOnly, statusCode: StatusCodes.Status501NotImplemented);

                await mutable.DeleteSecretAsync(path, ct);
                return Results.NoContent();
            })
            .WithMetadata(SecretOperations.Delete).LoadsNoResource()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    /// <summary>
    /// Maps the administration route of the secrets module, relative to <paramref name="endpoints"/>: the purge of
    /// a compartment, <c>DELETE secrets/compartments/{compartment}</c>, which answers the number of secrets deleted.
    /// The actor recorded is the one the host authenticated, read from the request context. A host that does not
    /// map it has no purge over HTTP.
    /// </summary>
    /// <param name="endpoints">The builder to map the route on — the application, or a route group.</param>
    /// <returns>The group holding the route, for the host's authorization and further conventions.</returns>
    public static RouteGroupBuilder MapSecretsAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = Group(endpoints);

        group.MapDelete("/compartments/{compartment}", async (
                Compartment compartment, IRequestContext context, ISecretsPurge? purge, CancellationToken ct) =>
            {
                if (purge is null)
                    return Results.Problem("This vault cannot purge a compartment.", statusCode: StatusCodes.Status501NotImplemented);
                return Results.Ok(await purge.PurgeCompartmentAsync(compartment, context.Actor, ct));
            })
            .WithMetadata(SecretOperations.Purge).LoadsNoResource()
            .Produces<int>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    private static RouteGroupBuilder Group(IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);
        return endpoints.MapGroup("secrets")
            .BelongsTo<SecretsModule>()
            .RequireRequestContext()
            .ActsOnSpaces()
            .AddErrorTranslation();
    }

    private static IResult InvalidPath()
        => Result.Failure(Error.Validation("SecretPath.Invalid", "A secret path of the form {space}.{module}.{name}… is required.")).ToHttpResult();
}
