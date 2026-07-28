using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollisionDocNet.Cli;

internal static class CliJson
{
    internal static byte[] SerializeBundle(BundleDocument value) => JsonSerializer.SerializeToUtf8Bytes(value, CliJsonContext.Default.BundleDocument);
    internal static string Serialize(DetectionEnvelope value) => JsonSerializer.Serialize(value, CliJsonContext.Default.DetectionEnvelope);
    internal static string Serialize(CompletionEnvelope value) => JsonSerializer.Serialize(value, CliJsonContext.Default.CompletionEnvelope);
    internal static string Serialize(VersionEnvelope value) => JsonSerializer.Serialize(value, CliJsonContext.Default.VersionEnvelope);
    internal static string Serialize(HelpEnvelope value) => JsonSerializer.Serialize(value, CliJsonContext.Default.HelpEnvelope);
}

internal sealed record DetectionEnvelope(string SchemaVersion, string DetectedContainer, string DetectedFormat, string Outcome, string? SourceHash);
internal sealed record CompletionEnvelope(string SchemaVersion, string Outcome, string? ResultPath);
internal sealed record VersionEnvelope(string Product, string Version, string SchemaVersion);
internal sealed record HelpEnvelope(string Product, string Usage);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(BundleDocument))]
[JsonSerializable(typeof(DetectionEnvelope))]
[JsonSerializable(typeof(CompletionEnvelope))]
[JsonSerializable(typeof(VersionEnvelope))]
[JsonSerializable(typeof(HelpEnvelope))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
