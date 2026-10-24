using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orvano.Server;

/// <summary>
/// Reads and writes an <see cref="Optional{T}"/> as its value (spec 0014 JSW-10). Generated request records put it on
/// each partial update field, with <c>JsonIgnoreCondition.WhenWritingDefault</c> so an unset field is left out. It
/// reads and writes <typeparamref name="T"/> through the options' metadata, so it is trim and AOT safe; the generated
/// JSON context lists every such <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The field's type.</typeparam>
internal sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonSerializer.Deserialize(ref reader, Info(options))!);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.Value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value.Value, Info(options));
    }

    private static JsonTypeInfo<T> Info(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    }
}
