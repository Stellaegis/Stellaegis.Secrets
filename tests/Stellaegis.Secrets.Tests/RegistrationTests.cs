using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stellaegis.Secrets.PostgreSQL;
using Stellaegis.Secrets.Scaleway;

namespace Stellaegis.Secrets.Tests;

public sealed class RegistrationTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => KeyValuePair.Create<string, string?>(e.Key, e.Value)))
            .Build();

    [Fact]
    public void PostgreSQL_options_bind_from_the_default_section()
    {
        var services = new ServiceCollection().AddDatabaseSecretsProvider(Configuration(
            ("Stellaegis:Secrets:ConnectionString", "Host=db"),
            ("Stellaegis:Secrets:ActiveKeySlot", "2"),
            ("Stellaegis:Secrets:Keys:1", "one"),
            ("Stellaegis:Secrets:Keys:2", "two")));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<DatabaseSecretsOptions>>().Value;

        Assert.Equal("Host=db", options.ConnectionString);
        Assert.Equal(2, options.ActiveKeySlot);
        Assert.Equal(new Dictionary<int, string> { [1] = "one", [2] = "two" }, options.Keys);
    }

    [Fact]
    public void PostgreSQL_options_bind_from_another_section()
    {
        var services = new ServiceCollection().AddDatabaseSecretsProvider(
            Configuration(("Vault:ConnectionString", "Host=db")), sectionName: "Vault");

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<DatabaseSecretsOptions>>().Value;

        Assert.Equal("Host=db", options.ConnectionString);
    }

    [Fact]
    public void PostgreSQL_registers_one_provider_that_also_purges()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddDatabaseSecretsProvider(Configuration(
                ("Stellaegis:Secrets:ConnectionString", "Host=db"),
                ("Stellaegis:Secrets:Keys:1", "one")))
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IMutableSecretsProvider>(), provider.GetRequiredService<ISecretsPurge>());
    }

    [Fact]
    public void Scaleway_options_bind_from_the_default_section()
    {
        var services = new ServiceCollection().AddScalewaySecretsProvider(Configuration(
            ("Stellaegis:Secrets:Scaleway:ProjectId", "project"),
            ("Stellaegis:Secrets:Scaleway:Region", "nl-ams")));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ScalewaySecretsOptions>>().Value;

        Assert.Equal("project", options.ProjectId);
        Assert.Equal("nl-ams", options.Region);
    }
}
