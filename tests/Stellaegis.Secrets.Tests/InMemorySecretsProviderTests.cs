using Microsoft.Extensions.Logging.Abstractions;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets.Tests;

public sealed class InMemorySecretsProviderTests
{
    [Fact]
    public async Task A_purge_deletes_every_secret_of_its_compartment_and_nothing_else()
    {
        var provider = new InMemorySecretsProvider(NullLogger<InMemorySecretsProvider>.Instance);
        var ct       = TestContext.Current.CancellationToken;
        var acme     = Compartment.Parse("acme");

        SecretPath[] purged = [SecretPath.For<BillingModule>(acme, "a"), SecretPath.For<DesktopModule>(acme, "b")];
        SecretPath[] kept =
        [
            SecretPath.For<BillingModule>(Compartment.Parse("acme-2"), "a"),
            SecretPath.For<BillingModule>(Space.Shared, "a"),
            SecretPath.ForAssembly<BillingModule>("a"),
        ];
        foreach (var path in purged.Concat(kept))
            provider.AddSecret(path, "value");

        Assert.Equal(2, await provider.PurgeCompartmentAsync(acme, Actor.Parse("job:purge"), ct));

        Assert.Empty(await provider.GetSecretsAsync(purged, ct));
        Assert.Equal(kept.Length, (await provider.GetSecretsAsync(kept, ct)).Count);
        Assert.Equal(0, await provider.PurgeCompartmentAsync(acme, Actor.Parse("job:purge"), ct));
    }
}
