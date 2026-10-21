using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets;

/// <summary>
/// The identity of the secrets module, <c>secrets</c>: what its routes belong to, and what the application
/// declares it as. A vault serves an application without compartments, by the application's own secrets under
/// <see cref="SecretPath.AssemblySegment"/>, as well as one with, by the secrets of each space.
/// </summary>
public sealed class SecretsModule : IStellaegisModule
{
    /// <inheritdoc/>
    public static string Name => "secrets";

    /// <summary>
    /// What the module declares to the application: optionally compartmented, serving the shared space, its four
    /// operations, and compatible with an isolation by owner — no secret belongs to an actor, so the isolation
    /// holds by nature.
    /// </summary>
    public static ModuleDeclaration Declaration { get; } = ModuleDeclaration.OptionallyCompartmented<SecretsModule>()
        .AcceptingSharedSpace()
        .WithOperations(SecretOperations.Read, SecretOperations.Write, SecretOperations.Delete, SecretOperations.Purge)
        .WithCapability(ModuleCapability.OwnerIsolation);
}

/// <summary>The operations of the secrets module, which its routes are labelled with.</summary>
public static class SecretOperations
{
    /// <summary>Reading one or several secrets, <c>secrets.read</c>.</summary>
    public static readonly Operation Read = new("secrets", "read", OperationKind.Read);

    /// <summary>Creating or replacing a secret, <c>secrets.write</c>.</summary>
    public static readonly Operation Write = new("secrets", "write", OperationKind.Write);

    /// <summary>Removing a secret, <c>secrets.delete</c>.</summary>
    public static readonly Operation Delete = new("secrets", "delete", OperationKind.Delete);

    /// <summary>Purging every secret of a compartment, <c>secrets.purge</c> — an administration route.</summary>
    public static readonly Operation Purge = new("secrets", "purge", OperationKind.Delete);
}

/// <summary>Registers the secrets module itself, whatever provider holds the secrets.</summary>
public static class SecretsServiceCollectionExtensions
{
    /// <summary>
    /// Declares the secrets module to the application (<see cref="SecretsModule.Declaration"/>). Every provider's
    /// registration calls it; calling it again registers nothing more.
    /// </summary>
    public static IServiceCollection AddStellaegisSecrets(this IServiceCollection services)
    {
        Guard.NotNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(SecretsModuleRegistered)))
            return services;
        services.AddSingleton(SecretsModuleRegistered.Instance);
        return services.AddStellaegisModule(SecretsModule.Declaration);
    }

    private sealed class SecretsModuleRegistered
    {
        public static readonly SecretsModuleRegistered Instance = new();
    }
}
