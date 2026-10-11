using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stellaegis.Secrets;

/// <summary>
/// Serializes a <see cref="SecretPath"/> as its text form <c>a.b.c</c>, as a value and as a
/// dictionary key.
/// </summary>
internal sealed class SecretPathJsonConverter : JsonConverter<SecretPath>
{
    public override SecretPath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        FromText(reader.GetString());

    public override void Write(Utf8JsonWriter writer, SecretPath value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    public override SecretPath ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        FromText(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SecretPath value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.ToString());

    private static SecretPath FromText(string? text) =>
        SecretPath.TryParse(text, out var path)
            ? path
            : throw new JsonException($"Invalid secret path '{text}'.");
}
