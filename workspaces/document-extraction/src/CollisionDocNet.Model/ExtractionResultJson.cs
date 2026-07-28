using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollisionDocNet.Model;

public static class ExtractionResultJson
{
    public static byte[] SerializeToUtf8Bytes(ExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.SerializeToUtf8Bytes(
            result,
            ExtractionResultJsonContext.Default.ExtractionResult);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ExtractionResult))]
internal sealed partial class ExtractionResultJsonContext : JsonSerializerContext;
