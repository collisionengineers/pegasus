using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Intake;

namespace Pegasus.JevMailEvaluation;

public static class EvaluationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteJsonLinesAsync<T>(string path, IEnumerable<T> values, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        foreach (var value in values)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(value, Options).AsMemory(), cancellationToken);
        }
    }

    public static IEnumerable<T> ReadJsonLines<T>(string path)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(line, Options);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Malformed JSONL at {path}:{lineNumber}.", exception);
            }

            yield return value ?? throw new InvalidDataException($"Null JSONL value at {path}:{lineNumber}.");
        }
    }
}

public sealed record CohortEntry(
    string Id,
    string SourcePath,
    string Sha256,
    string Stratum,
    long ByteLength,
    int AttachmentCount);

public sealed record GoldLabel(
    string Id,
    string Family,
    string? Subtype,
    bool IsAmbiguous,
    string Reason,
    string ReviewedAtUtc);

public sealed record EvidenceAttachment(
    string FileName,
    string MediaType,
    long? ByteLength,
    string? ExtractedText,
    bool TextTruncated);

public sealed record JevMailState(
    string? Sender,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string? Subject,
    string? BodyPlainText,
    bool BodyTruncated,
    string? InReplyTo,
    IReadOnlyList<string> References,
    IReadOnlyList<EvidenceAttachment> Attachments,
    bool RequiresOcr,
    bool IsIncomplete,
    IReadOnlyList<string> ReaderIssues,
    bool AttachmentsOmittedForAblation);

public sealed record ChoiceAnswer(
    string Choice,
    IReadOnlyDictionary<string, double> Probabilities,
    double Confidence);

public sealed record JevPrediction(
    string Id,
    string Condition,
    int Repeat,
    string RequestedModel,
    string ActualModel,
    string PromptVersion,
    string StateSha256,
    string Family,
    string? Subtype,
    double Confidence,
    IReadOnlyDictionary<string, double> FamilyProbabilities,
    IReadOnlyDictionary<string, double>? SubtypeProbabilities,
    int InputTokens,
    int OutputTokens,
    long LatencyMilliseconds,
    string RecordedAtUtc);

public sealed record EvaluationFailure(
    string Id,
    string Condition,
    int Repeat,
    string Stage,
    string ErrorType,
    string Message,
    string RecordedAtUtc);

public static class CorpusPath
{
    public static string Resolve(string corpusRoot, string relativePath)
    {
        var root = Path.GetFullPath(corpusRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A cohort source path resolves outside the corpus root.");
        }
        return candidate;
    }
}

public static class EvaluationTaxonomy
{
    public const string Abstain = "unclassified-or-ambiguous";

    public static readonly IReadOnlyDictionary<string, string[]> Received =
        Enum.GetValues<ReceivedMailFamily>().ToDictionary(
            MailTaxonomy.CategoryName,
            family => MailTaxonomy.ConfirmedReceivedSubtypes[family].ToArray(),
            StringComparer.Ordinal);

    public static void Validate(GoldLabel label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label.Reason);
        if (label.Family == Abstain)
        {
            if (label.Subtype is not null || !label.IsAmbiguous)
            {
                throw new InvalidDataException("An abstention needs IsAmbiguous=true and no subtype.");
            }
            return;
        }

        if (!Received.TryGetValue(label.Family, out var subtypes))
        {
            throw new InvalidDataException($"Unknown Received family '{label.Family}'.");
        }

        if (label.Subtype is not null && !subtypes.Contains(label.Subtype, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Subtype '{label.Subtype}' is not valid for '{label.Family}'.");
        }
    }

    public static string Detailed(string family, string? subtype) =>
        subtype is null ? family : $"{family}/{subtype}";
}
