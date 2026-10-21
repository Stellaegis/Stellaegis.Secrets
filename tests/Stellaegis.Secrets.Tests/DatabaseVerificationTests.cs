using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Data;
using Stellaegis.Primitives.Hosting;
using Stellaegis.Secrets.PostgreSQL;

namespace Stellaegis.Secrets.Tests;

/// <summary>The vault installed by the fixture, verified as an application does at startup, under its own role.</summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseVerificationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task The_installed_vault_passes_the_verification_of_the_application()
    {
        Assert.SkipWhen(database.Unavailable is not null, database.Unavailable ?? "");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            KeyValuePair.Create<string, string?>("Stellaegis:Secrets:ConnectionString", database.ConnectionString),
            KeyValuePair.Create<string, string?>("Stellaegis:Secrets:Keys:1", "one"),
        ]).Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddStellaegisFrame(ApplicationFrame.ManyCompartments())
            .AddDatabaseSecretsProvider(configuration)
            .BuildServiceProvider();

        provider.ValidateStellaegisModules();
        await provider.VerifyStellaegisDatabasesAsync(TestContext.Current.CancellationToken);

        Assert.Equal([SecretsDatabase.Value], provider.GetStellaegisModuleDatabases());
    }
}
