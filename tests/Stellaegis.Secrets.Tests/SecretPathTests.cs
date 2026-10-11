using System.Text.Json;

namespace Stellaegis.Secrets.Tests;

public sealed class SecretPathTests
{
    [Fact]
    public void From_keeps_segments_in_order()
    {
        var path = SecretPath.From("tenantDB", "primary", "postgresql", "acme");

        Assert.Equal(["tenantDB", "primary", "postgresql", "acme"], path.Segments);
        Assert.Equal("tenantDB.primary.postgresql.acme", path.ToString());
    }

    [Fact]
    public void A_single_segment_is_a_path()
    {
        var path = SecretPath.From("smtp-password");

        Assert.Equal(["smtp-password"], path.Segments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("é")]
    public void From_refuses_an_invalid_segment(string segment)
    {
        Assert.Throws<ArgumentException>(() => SecretPath.From("valid", segment));
    }

    [Fact]
    public void From_refuses_no_segment()
    {
        Assert.Throws<ArgumentException>(() => SecretPath.From());
    }

    [Theory]
    [InlineData("tenantDB.primary.postgresql.acme")]
    [InlineData("smtp-password")]
    [InlineData("a_b.c-d.E9")]
    public void Parse_is_the_inverse_of_ToString(string text)
    {
        Assert.Equal(text, SecretPath.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("a..b")]
    [InlineData(".a")]
    [InlineData("a.")]
    [InlineData("a/b")]
    public void Parse_refuses_an_invalid_text(string text)
    {
        Assert.Throws<FormatException>(() => SecretPath.Parse(text));
        Assert.False(SecretPath.TryParse(text, out _));
    }

    [Fact]
    public void Paths_with_the_same_segments_are_equal()
    {
        var a = SecretPath.From("a", "b");
        var b = SecretPath.Parse("a.b");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Paths_compare_case_sensitively()
    {
        Assert.NotEqual(SecretPath.From("Acme"), SecretPath.From("acme"));
    }

    [Fact]
    public void Json_writes_the_text_form_as_value_and_as_key()
    {
        var path = SecretPath.From("a", "b");

        Assert.Equal("\"a.b\"", JsonSerializer.Serialize(path));
        Assert.Equal("{\"a.b\":\"v\"}", JsonSerializer.Serialize(new Dictionary<SecretPath, string> { [path] = "v" }));
    }

    [Fact]
    public void Json_reads_back_what_it_writes()
    {
        var map = new Dictionary<SecretPath, string> { [SecretPath.From("a", "b")] = "v" };

        var read = JsonSerializer.Deserialize<Dictionary<SecretPath, string>>(JsonSerializer.Serialize(map));

        Assert.Equal(map, read);
    }

    [Fact]
    public void Json_refuses_an_invalid_path()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SecretPath>("\"a..b\""));
    }
}
