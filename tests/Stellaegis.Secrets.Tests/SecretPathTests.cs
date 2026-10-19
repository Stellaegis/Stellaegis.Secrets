using System.Text.Json;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets.Tests;

public sealed class SecretPathTests
{
    private static readonly Compartment Acme = Compartment.Parse("acme");

    [Fact]
    public void For_puts_the_space_then_the_module_then_the_names()
    {
        var path = SecretPath.For<BillingModule>(Acme, "smtp", "password");

        Assert.Equal(["acme", "billing", "smtp", "password"], path.Segments);
        Assert.Equal("acme.billing.smtp.password", path.ToString());
    }

    [Fact]
    public void For_writes_the_shared_space_under_its_segment()
    {
        var path = SecretPath.For<BillingModule>(Space.Shared, "smtp");

        Assert.Equal("_shared.billing.smtp", path.ToString());
    }

    [Fact]
    public void ForAssembly_puts_the_secret_outside_every_space()
    {
        var path = SecretPath.ForAssembly<DesktopModule>("api-key");

        Assert.Equal("_assembly.desktop.api-key", path.ToString());
    }

    [Fact]
    public void For_refuses_a_null_space()
    {
        Assert.Throws<ArgumentNullException>(() => SecretPath.For<BillingModule>(null!, "smtp"));
    }

    [Fact]
    public void A_path_has_at_least_one_name()
    {
        Assert.Throws<ArgumentException>(() => SecretPath.For<BillingModule>(Acme));
        Assert.Throws<ArgumentException>(() => SecretPath.ForAssembly<DesktopModule>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("é")]
    public void A_name_must_be_a_valid_segment(string name)
    {
        Assert.Throws<ArgumentException>(() => SecretPath.For<BillingModule>(Acme, "valid", name));
    }

    [Fact]
    public void A_module_name_must_follow_the_module_name_rule()
    {
        Assert.Throws<ArgumentException>(() => SecretPath.ForAssembly<InvalidModule>("key"));
    }

    [Fact]
    public void The_prefix_of_a_compartment_holds_its_paths_and_no_other()
    {
        var prefix = SecretPath.TextPrefixOf(Acme);

        Assert.Equal("acme.", prefix);
        Assert.StartsWith(prefix, SecretPath.For<BillingModule>(Acme, "smtp").ToString(), StringComparison.Ordinal);
        Assert.DoesNotMatch("^acme\\.", SecretPath.For<BillingModule>(Compartment.Parse("acme-2"), "smtp").ToString());
        Assert.DoesNotMatch("^acme\\.", SecretPath.For<BillingModule>(Space.Shared, "smtp").ToString());
    }

    [Theory]
    [InlineData("acme.billing.smtp")]
    [InlineData("_shared.billing.smtp.password")]
    [InlineData("_assembly.desktop.api-key")]
    [InlineData("o0f8a.billing.a_b.C-d.E9")]
    public void Parse_is_the_inverse_of_ToString(string text)
    {
        Assert.Equal(text, SecretPath.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("acme.billing")]
    [InlineData("acme.billing..smtp")]
    [InlineData("acme.billing.smtp.")]
    [InlineData("Acme.billing.smtp")]
    [InlineData("_other.billing.smtp")]
    [InlineData("acme.in-valid.smtp")]
    [InlineData("acme.billing.a/b")]
    [InlineData("tenantDB.primary.postgresql.acme")]
    public void Parse_refuses_an_invalid_text(string text)
    {
        Assert.Throws<FormatException>(() => SecretPath.Parse(text));
        Assert.False(SecretPath.TryParse(text, out _));
    }

    [Fact]
    public void Paths_with_the_same_segments_are_equal()
    {
        var a = SecretPath.For<BillingModule>(Acme, "smtp");
        var b = SecretPath.Parse("acme.billing.smtp");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Names_compare_case_sensitively()
    {
        Assert.NotEqual(SecretPath.For<BillingModule>(Acme, "Smtp"), SecretPath.For<BillingModule>(Acme, "smtp"));
    }

    [Fact]
    public void Json_writes_the_text_form_as_value_and_as_key()
    {
        var path = SecretPath.For<BillingModule>(Acme, "smtp");

        Assert.Equal("\"acme.billing.smtp\"", JsonSerializer.Serialize(path));
        Assert.Equal("{\"acme.billing.smtp\":\"v\"}", JsonSerializer.Serialize(new Dictionary<SecretPath, string> { [path] = "v" }));
    }

    [Fact]
    public void Json_reads_back_what_it_writes()
    {
        var map = new Dictionary<SecretPath, string> { [SecretPath.For<BillingModule>(Acme, "smtp")] = "v" };

        var read = JsonSerializer.Deserialize<Dictionary<SecretPath, string>>(JsonSerializer.Serialize(map));

        Assert.Equal(map, read);
    }

    [Fact]
    public void Json_refuses_an_invalid_path()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SecretPath>("\"a.b\""));
    }
}
