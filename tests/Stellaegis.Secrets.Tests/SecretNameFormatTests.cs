using Stellaegis.Secrets.Scaleway;

namespace Stellaegis.Secrets.Tests;

public sealed class SecretNameFormatTests
{
    public static TheoryData<string> Paths => new()
    {
        "acme.billing.primary.postgresql",
        "_shared.billing.smtp-password",
        "_assembly.desktop.a_b.c-d",
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
        var path = SecretPath.Parse("acme.billing.primary.postgresql");

        Assert.Equal("/acme/billing/primary/postgresql", ScalewaySecretNameFormat.Instance.Join(path));
        Assert.Equal(("/acme/billing/primary", "postgresql"), ScalewaySecretNameFormat.FolderAndName(path));
    }

    [Theory]
    [InlineData("acme/billing/smtp")]
    [InlineData("/acme//billing/smtp")]
    [InlineData("/")]
    [InlineData("/acme/billing/a.b")]
    [InlineData("/acme/billing")]
    [InlineData("/tenantDB/primary/postgresql/acme")]
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
