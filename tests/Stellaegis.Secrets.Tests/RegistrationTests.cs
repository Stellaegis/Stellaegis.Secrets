using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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
            Configuration(("Vault:ConnectionString", "Host=db"), ("Vault:Keys:1", "one")), sectionName: "Vault");

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
            ("Stellaegis:Secrets:Scaleway:ApiToken", "token"),
            ("Stellaegis:Secrets:Scaleway:Region", "nl-ams")));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ScalewaySecretsOptions>>().Value;

        Assert.Equal("project", options.ProjectId);
        Assert.Equal("nl-ams", options.Region);
    }

    [Theory]
    [InlineData("", "one", "1", "ConnectionString")]
    [InlineData("Host=db", "", "1", "Keys:1")]
    [InlineData("Host=db", "one", "3", "ActiveKeySlot")]
    public async Task PostgreSQL_options_that_do_not_hold_fail_the_host_start(
        string connectionString, string key, string activeSlot, string named)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDatabaseSecretsProvider(Configuration(
            ("Stellaegis:Secrets:ConnectionString", connectionString),
            ("Stellaegis:Secrets:Keys:1", key),
            ("Stellaegis:Secrets:ActiveKeySlot", activeSlot)));
        await using var app = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(named, error.Message);
    }

    [Fact]
    public async Task Scaleway_options_without_a_token_fail_the_host_start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScalewaySecretsProvider(Configuration(("Stellaegis:Secrets:Scaleway:ProjectId", "project")));
        await using var app = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("ApiToken", error.Message);
    }
}
