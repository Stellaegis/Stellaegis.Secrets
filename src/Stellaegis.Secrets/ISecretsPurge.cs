using Stellaegis.Primitives;

namespace Stellaegis.Secrets;

/// <summary>
/// Removes every secret of a compartment, whatever module wrote it — an act of administration on the
/// compartment, for instance when a customer leaves.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="IMutableSecretsProvider"/>, which every module writes its secrets through: only the
/// code in charge of administration takes this interface, so a module that keeps its own secrets cannot erase
/// those of the others.
/// </para>
/// <para>
/// The PostgreSQL and in-memory providers implement it in process. Over HTTP, the client implements it against the
/// administration route a host maps with <c>MapSecretsAdminEndpoints</c>; the host then records the actor it
/// authenticated, not the one the client names.
/// </para>
/// </remarks>
public interface ISecretsPurge
{
    /// <summary>
    /// Deletes every secret under the prefix of <paramref name="compartment"/>, for every module. The shared space
    /// and the secrets of the application itself (<see cref="SecretPath.AssemblySegment"/>) are left untouched.
    /// </summary>
    /// <remarks>
    /// <para>Irreversible. A replayed purge completes an interrupted one, and deletes nothing once nothing is left.</para>
    /// <para>No event is published: the purge logs <paramref name="actor"/> when it starts and when it ends.</para>
    /// </remarks>
    /// <param name="compartment">The compartment whose secrets are deleted.</param>
    /// <param name="actor">Who purges — the administrator, or a task named after itself, such as <c>job:purge</c>.</param>
    /// <param name="cancellationToken">Cancels the purge; what is deleted stays deleted.</param>
    /// <returns>The number of secrets deleted by this call.</returns>
    Task<int> PurgeCompartmentAsync(Compartment compartment, Actor actor, CancellationToken cancellationToken = default);
}
