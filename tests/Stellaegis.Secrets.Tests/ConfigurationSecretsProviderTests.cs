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
        var provider = Provider(ConfigurationSecretsProvider.SectionName, ("tenantDB.primary.postgresql.acme", "s3cret"));

        Assert.Equal("s3cret", await provider.GetSecretAsync(SecretPath.Parse("tenantDB.primary.postgresql.acme"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_a_secret_under_another_section()
    {
        var provider = Provider("Mine", ("a.b", "v"));

        Assert.Equal("v", await provider.GetSecretAsync(SecretPath.Parse("a.b"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_secret_throws_on_single_read_and_is_absent_from_batch_read()
    {
        var provider = Provider(ConfigurationSecretsProvider.SectionName, ("a.b", "v"));
        var ct       = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetSecretAsync(SecretPath.Parse("a.c"), ct));

        var batch = await provider.GetSecretsAsync([SecretPath.Parse("a.b"), SecretPath.Parse("a.c")], ct);
        Assert.Equal(new Dictionary<SecretPath, string> { [SecretPath.Parse("a.b")] = "v" }, batch);
    }
}
