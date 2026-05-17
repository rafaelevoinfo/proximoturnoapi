using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProximoTurnoApi.Application.Converters;

public class TimeOnlyJsonConverter : JsonConverter<TimeOnly> {
    private const string TimeFormat = "HH:mm:ss";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value)) return default;

        if (TimeOnly.TryParseExact(value, ["HH:mm:ss", "HH:mm"], out var result)) {
            return result;
        }

        throw new JsonException($"Format '{value}' is invalid for TimeOnly. Expected HH:mm or HH:mm:ss.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString(TimeFormat));
    }
}