using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProximoTurnoApi.Application.Converters;

public class DateOnlyJsonConverter : JsonConverter<DateOnly> {
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value)) return default;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", out var result)) {
            return result;
        }

        throw new JsonException($"Format '{value}' is invalid for DateOnly. Expected yyyy-MM-dd.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(DateFormat));
    }
}