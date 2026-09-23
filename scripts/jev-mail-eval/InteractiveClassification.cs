using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Pegasus.JevMailEvaluation;

public sealed record SingleMailClassification(JevMailState Evidence, JevPrediction Prediction);

public sealed class JevSingleMailClassifier(TypeSafeSystemOneClient client, EvidenceStateBuilder stateBuilder)
{
    public async Task<SingleMailClassification> ClassifyAsync(string emlPath, CancellationToken cancellationToken = default)
    {
        var path = ResolveEmlPath(emlPath);
        var state = await stateBuilder.BuildAsync(path, false, cancellationToken);
        var stateJson = JsonSerializer.Serialize(state, EvaluationJson.Options);
        var stateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateJson)));
        var familyResponse = await client.AskChoiceAsync(
            state, JevQuestions.FamilyInstruction, JevQuestions.Families, cancellationToken);

        var family = familyResponse.Answer.Choice;
        string? subtype = null;
        IReadOnlyDictionary<string, double>? subtypeProbabilities = null;
        var inputTokens = familyResponse.InputTokens;
        var outputTokens = familyResponse.OutputTokens;
        var latency = familyResponse.LatencyMilliseconds;

        if (family != EvaluationTaxonomy.Abstain && EvaluationTaxonomy.Received[family].Length > 0)
        {
            var subtypeResponse = await client.AskChoiceAsync(
                state,
                $"Assuming the Received family is `{family}`, choose its supported subtype. Choose {EvaluationTaxonomy.Abstain} when none is supported.",
                JevQuestions.Subtypes(family),
                cancellationToken);
            if (!string.Equals(familyResponse.Model, subtypeResponse.Model, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The TypeSafe model alias changed within one hierarchical evaluation.");
            }

            subtype = subtypeResponse.Answer.Choice == EvaluationTaxonomy.Abstain ? null : subtypeResponse.Answer.Choice;
            subtypeProbabilities = subtypeResponse.Answer.Probabilities;
            inputTokens += subtypeResponse.InputTokens;
            outputTokens += subtypeResponse.OutputTokens;
            latency += subtypeResponse.LatencyMilliseconds;
        }

        var id = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path, cancellationToken)))[..16].ToLowerInvariant();
        var prediction = new JevPrediction(
            id, "hierarchical", 0, "jev-latest", familyResponse.Model, JevQuestions.PromptVersion, stateHash,
            family, subtype, familyResponse.Answer.Confidence, familyResponse.Answer.Probabilities, subtypeProbabilities,
            inputTokens, outputTokens, latency, DateTimeOffset.UtcNow.ToString("O"));
        return new(state, prediction);
    }

    public static string ResolveEmlPath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(trimmed))
        {
            throw new ArgumentException("Enter a full path to an .eml file.");
        }

        var resolved = Path.GetFullPath(trimmed);
        if (!string.Equals(Path.GetExtension(resolved), ".eml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The selected file must have an .eml extension.");
        }
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException("The .eml file does not exist.", resolved);
        }
        return resolved;
    }
}

public static class PlainTextClassificationFormatter
{
    public static string Format(string path, SingleMailClassification result)
    {
        var state = result.Evidence;
        var prediction = result.Prediction;
        var text = new StringBuilder()
            .AppendLine()
            .AppendLine("JEV MAIL CLASSIFICATION")
            .AppendLine(new string('=', 23));
        Add($"File:       {path}");
        Add($"Subject:    {state.Subject ?? "(none)"}");
        Add($"From:       {state.Sender ?? "(unknown)"}");
        text.AppendLine();
        Add($"Category:   {Friendly(prediction.Family)}");
        Add($"Subtype:    {(prediction.Subtype is null ? "(none)" : Friendly(prediction.Subtype))}");
        Add($"Confidence: {prediction.Confidence * 100:F1}%");

        var alternatives = prediction.FamilyProbabilities
            .Where(item => item.Key != prediction.Family)
            .OrderByDescending(item => item.Value)
            .Take(3)
            .ToArray();
        if (alternatives.Length > 0)
        {
            text.AppendLine("Alternatives:");
            foreach (var alternative in alternatives)
            {
                Add($"  {Friendly(alternative.Key),-30} {alternative.Value * 100:F1}%");
            }
        }

        text.AppendLine();
        Add($"Attachments: {state.Attachments.Count}");
        Add($"Needs OCR:   {(state.RequiresOcr ? "yes" : "no")}");
        Add($"Evidence:    {(state.IsIncomplete ? "incomplete" : "complete")}");
        foreach (var issue in state.ReaderIssues)
        {
            Add($"  Warning: {issue}");
        }

        text.AppendLine();
        Add($"Model:       {prediction.ActualModel}");
        Add($"Tokens:      {prediction.InputTokens} input, {prediction.OutputTokens} output");
        Add($"Latency:     {prediction.LatencyMilliseconds} ms");
        return text.ToString();

        void Add(FormattableString line) => text.AppendLine(line.ToString(CultureInfo.InvariantCulture));
    }

    internal static string Friendly(string value) => string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries)
        .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
