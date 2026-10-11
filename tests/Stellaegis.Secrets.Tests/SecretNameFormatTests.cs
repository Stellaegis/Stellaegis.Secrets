using Stellaegis.Secrets.Scaleway;

namespace Stellaegis.Secrets.Tests;

public sealed class SecretNameFormatTests
{
    public static TheoryData<string> Paths => new()
    {
        "tenantDB.primary.postgresql.acme",
        "smtp-password",
        "a_b.c-d",
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public void Dotted_format_round_trips(string text) => AssertRoundTrip(DottedSecretNameFormat.Instance, text);

    [Theory]
    [MemberData(nameof(Paths))]
    public void Scaleway_format_round_trips(string text) => AssertRoundTrip(ScalewaySecretNameFormat.Instance, text);

    [Fact]
    public void Scaleway_puts_every_segment_but_the_last_in_folders()
    {
        var path = SecretPath.Parse("tenantDB.primary.postgresql.acme");

        Assert.Equal("/tenantDB/primary/postgresql/acme", ScalewaySecretNameFormat.Instance.Join(path));
        Assert.Equal(("/tenantDB/primary/postgresql", "acme"), ScalewaySecretNameFormat.FolderAndName(path));
    }

    [Fact]
    public void Scaleway_puts_a_single_segment_at_the_root()
    {
        var path = SecretPath.From("smtp-password");

        Assert.Equal("/smtp-password", ScalewaySecretNameFormat.Instance.Join(path));
        Assert.Equal(("/", "smtp-password"), ScalewaySecretNameFormat.FolderAndName(path));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("/a//b")]
    [InlineData("/")]
    [InlineData("/a.b")]
    public void Scaleway_refuses_a_name_that_is_not_a_path(string nativeName)
    {
        Assert.Throws<FormatException>(() => ScalewaySecretNameFormat.Instance.Split(nativeName));
    }

    private static void AssertRoundTrip(ISecretNameFormat format, string text)
    {
        var path = SecretPath.Parse(text);

        Assert.Equal(path, format.Split(format.Join(path)));
    }
}
