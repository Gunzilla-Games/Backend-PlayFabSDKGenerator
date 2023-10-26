using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayFab.Json;

public class TextJsonSerializerPlugin : ISerializerPlugin
{
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ValueTask<T?> DeserializeAsync<T>(Stream serialized)
    {
        return JsonSerializer.DeserializeAsync<T>(serialized, JsonSerializerOptions);
    }

    public string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonSerializerOptions);
    }
}