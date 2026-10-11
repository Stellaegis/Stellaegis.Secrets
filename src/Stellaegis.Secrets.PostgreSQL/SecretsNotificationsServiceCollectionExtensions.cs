using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Backplane;
using Stellaegis.Backplane.SecretEvents;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Registration for publishing <c>secret.changed</c> events from a mutable vault.
/// </summary>
public static class SecretsNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="IMutableSecretsProvider"/> so every write publishes a
    /// <c>SecretChangedEvent</c> on the backplane. No-op when the vault is read-only (no mutable provider).
    /// Requires a backplane transport and <c>AddStellaegisSecretsBackplaneEvents()</c>; call after the
    /// secrets provider registration.
    /// </summary>
    public static IServiceCollection AddStellaegisSecretChangeNotifications(this IServiceCollection services)
    {
        var existing = services.LastOrDefault(d => d.ServiceType == typeof(IMutableSecretsProvider));
        if (existing is null)
            return services; // Read-only vault — nothing writes, nothing to publish.

        services.AddStellaegisSecretsBackplaneEvents();
        services.Remove(existing);
        services.Add(new ServiceDescriptor(typeof(IMutableSecretsProvider), sp =>
        {
            var inner = (IMutableSecretsProvider)(
                existing.ImplementationInstance
                ?? existing.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!));
            return new PublishingSecretsProvider(inner, sp.GetRequiredService<IBackplane>());
        }, existing.Lifetime));

        return services;
    }
}
