using System.Text.Json;
using System.Text.Json.Serialization;

namespace ViaPost.Models;

/// <summary>A one-time secret that is revealed only through an explicit method call.</summary>
[JsonConverter(typeof(SensitiveStringJsonConverter))]
public sealed class SensitiveString
{
    private readonly string _value;

    private SensitiveString(string value) => _value = value;

    public static SensitiveString Empty { get; } = new(string.Empty);

    public static SensitiveString From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 ? Empty : new SensitiveString(value);
    }

    public string Reveal() => _value;

    public override string ToString() => "[REDACTED]";
}

public sealed class SensitiveStringJsonConverter : JsonConverter<SensitiveString>
{
    public override SensitiveString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : SensitiveString.From(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, SensitiveString value, JsonSerializerOptions options) =>
        writer.WriteStringValue("[REDACTED]");
}
