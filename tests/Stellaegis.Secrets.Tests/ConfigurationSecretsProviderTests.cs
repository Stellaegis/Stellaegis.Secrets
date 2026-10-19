using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Stellaegis.Secrets.Tests;

public sealed class ConfigurationSecretsProviderTests
{
    private static ConfigurationSecretsProvider Provider(string section, params (string Key, string Value)[] entries)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => KeyValuePair.Create<string, string?>($"{section}:{e.Key}", e.Value)))
            .Build();

        return section == ConfigurationSecretsProvider.SectionName
            ? new ConfigurationSecretsProvider(configuration, NullLogger<ConfigurationSecretsProvider>.Instance)
            : new ConfigurationSecretsProvider(configuration, NullLogger<ConfigurationSecretsProvider>.Instance, section);
    }

    [Fact]
    public async Task Reads_a_secret_under_the_default_section()
    {
        var provider = Provider(ConfigurationSecretsProvider.SectionName, ("acme.billing.primary.postgresql", "s3cret"));

        Assert.Equal("s3cret", await provider.GetSecretAsync(SecretPath.Parse("acme.billing.primary.postgresql"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_a_secret_under_another_section()
    {
        var provider = Provider("Mine", ("_assembly.desktop.b", "v"));

        Assert.Equal("v", await provider.GetSecretAsync(SecretPath.Parse("_assembly.desktop.b"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_secret_throws_on_single_read_and_is_absent_from_batch_read()
    {
        var provider = Provider(ConfigurationSecretsProvider.SectionName, ("_assembly.desktop.b", "v"));
        var ct       = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetSecretAsync(SecretPath.Parse("_assembly.desktop.c"), ct));

        var batch = await provider.GetSecretsAsync([SecretPath.Parse("_assembly.desktop.b"), SecretPath.Parse("_assembly.desktop.c")], ct);
        Assert.Equal(new Dictionary<SecretPath, string> { [SecretPath.Parse("_assembly.desktop.b")] = "v" }, batch);
    }
}
