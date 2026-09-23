using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pegasus.JevMailEvaluation;

public sealed record JevResponse(string Model, ChoiceAnswer Answer, int InputTokens, int OutputTokens, long LatencyMilliseconds);

public sealed class TypeSafeSystemOneClient(HttpClient httpClient, string apiKey)
{
    public const string Endpoint = "https://api.typesafe.ai/v1/systemone";

    public async Task<JevResponse> AskChoiceAsync(
        object state,
        string instructions,
        IReadOnlyDictionary<string, string> criteria,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = "jev-latest",
            state,
            questions = new Dictionary<string, object>
            {
                ["classification"] = new { type = "choice", instructions, criteria }
            }
        };
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, EvaluationJson.Options), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529 && attempt < 3)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"TypeSafe returned {(int)response.StatusCode} {response.ReasonPhrase}.", null, response.StatusCode);
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var answer = root.GetProperty("answers").GetProperty("classification");
            var probabilities = answer.GetProperty("probabilities").EnumerateObject()
                .ToDictionary(item => item.Name, item => item.Value.GetDouble(), StringComparer.Ordinal);
            stopwatch.Stop();
            return new(
                root.GetProperty("model").GetString() ?? throw new InvalidDataException("TypeSafe response omitted model."),
                new(
                    answer.GetProperty("choice").GetString() ?? throw new InvalidDataException("TypeSafe response omitted choice."),
                    probabilities,
                    answer.GetProperty("confidence").GetDouble()),
                root.GetProperty("usage").GetProperty("input_tokens").GetInt32(),
                root.GetProperty("usage").GetProperty("output_tokens").GetInt32(),
                stopwatch.ElapsedMilliseconds);
        }
    }
}

public static class JevQuestions
{
    public const string PromptVersion = "jev-mail-v1";
    public const string FamilyInstruction =
        "Classify this exact received email into one Pegasus Received family. Use current message evidence and attachment evidence. Quoted or attached old content is not fresh work. If evidence is missing, contradictory, or several families genuinely compete, choose unclassified-or-ambiguous.";
    public const string FlatInstruction =
        "Classify this exact received email into one detailed Pegasus category. Use current message evidence and attachment evidence. Quoted or attached old content is not fresh work. If evidence does not support exactly one category, choose unclassified-or-ambiguous.";

    public static readonly IReadOnlyDictionary<string, string> Families = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["General"] = "Automatic replies, delivery failures, acknowledgements, general chases, or informational case summaries.",
        ["billing"] = "Payments, remittances, invoices, or billing questions.",
        ["new-instruction-received"] = "Initial accepted work instruction: audit, diminution, inspection, new client, or website enquiry.",
        ["non-client-related"] = "Internal, company, tool, service, or software mail unrelated to client work.",
        ["in-progress-cases"] = "Cancellation, progress update, chase, or other correspondence about ongoing work.",
        ["post-report-emails"] = "Query, dispute, or amendment request about a delivered report.",
        ["pre-instruction-emails"] = "Triage, another pre-formal handling request, or images before a formal instruction.",
        ["internal-cc"] = "Internal copied correspondence that is not the primary actionable occurrence.",
        [EvaluationTaxonomy.Abstain] = "Evidence is missing, unsupported, contradictory, or several categories genuinely compete."
    };

    public static IReadOnlyDictionary<string, string> Flat()
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (family, subtypes) in EvaluationTaxonomy.Received)
        {
            if (subtypes.Length == 0)
            {
                options[family] = Families[family];
            }
            else
            {
                foreach (var subtype in subtypes)
                {
                    options[$"{family}/{subtype}"] = DescribeSubtype(family, subtype);
                }
            }
        }
        options[EvaluationTaxonomy.Abstain] = Families[EvaluationTaxonomy.Abstain];
        return options;
    }

    public static IReadOnlyDictionary<string, string> Subtypes(string family)
    {
        var result = EvaluationTaxonomy.Received[family]
            .ToDictionary(subtype => subtype, subtype => DescribeSubtype(family, subtype), StringComparer.Ordinal);
        result[EvaluationTaxonomy.Abstain] = "No subtype in this family is supported by the evidence.";
        return result;
    }

    private static string DescribeSubtype(string family, string subtype) => (family, subtype) switch
    {
        ("General", "autoreply") => "Generated automatic reply.",
        ("General", "undeliverable") => "Delivery-status or non-delivery evidence.",
        ("General", "acknowledgement") => "Acknowledges receipt without a new request.",
        ("General", "general-chase") => "General chase, possibly about several cases.",
        ("General", "case-summary") => "Informational summary with no actionable request.",
        ("billing", "payment-notification") => "Payment notification without a question.",
        ("billing", "remittance") => "Remittance advice or evidence.",
        ("billing", "invoice-request") => "Requests an invoice or invoice action.",
        ("billing", "billing-query") => "Asks a billing, invoice, payment, or remittance question.",
        ("billing", "general-billing") => "Other billing correspondence.",
        ("new-instruction-received", "audit") => "Initial accepted Audit instruction.",
        ("new-instruction-received", "diminution") => "Initial accepted diminution instruction.",
        ("new-instruction-received", "inspection") => "Initial accepted vehicle inspection instruction.",
        ("new-instruction-received", "new-client") => "Initial work from a client without an accepted provider route.",
        ("new-instruction-received", "website-enquiry") => "Website-origin enquiry.",
        ("in-progress-cases", "cancellation") => "Explicit cancellation of ongoing work.",
        ("in-progress-cases", "case-update") => "Update on ongoing work.",
        ("in-progress-cases", "client-chasing-for-update") => "Client asks for progress.",
        ("in-progress-cases", "provider-chasing-for-update") => "Provider asks for progress.",
        ("in-progress-cases", "ongoing-correspondence") => "Other ongoing case correspondence.",
        ("post-report-emails", "query") => "Question about a delivered report.",
        ("post-report-emails", "dispute") => "Challenge to a delivered report or finding.",
        ("post-report-emails", "amendment-request") => "Request to amend a delivered report.",
        ("pre-instruction-emails", "triage-request") => "Initial assessment before a formal instruction.",
        ("pre-instruction-emails", "pre-formal-instruction-request") => "Pre-formal handling request other than triage.",
        ("pre-instruction-emails", "images-received") => "Images sent before a formal instruction.",
        _ => subtype
    };
}

public sealed class JevEvaluationRunner(TypeSafeSystemOneClient client, EvidenceStateBuilder stateBuilder)
{
    public async Task<IReadOnlyList<JevPrediction>> RunAsync(
        string corpusRoot,
        IReadOnlyList<CohortEntry> cohort,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var existing = File.Exists(outputPath)
            ? EvaluationJson.ReadJsonLines<JevPrediction>(outputPath).ToList()
            : [];
        var failuresPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputPath))!,
            $"{Path.GetFileNameWithoutExtension(outputPath)}.failures.jsonl");
        var failures = File.Exists(failuresPath)
            ? EvaluationJson.ReadJsonLines<EvaluationFailure>(failuresPath).ToList()
            : [];
        EnsureSingleModel(existing);
        foreach (var entry in cohort)
        {
            var path = CorpusPath.Resolve(corpusRoot, entry.SourcePath);
            JevMailState state;
            try
            {
                state = await stateBuilder.BuildAsync(path, false, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await RecordFailureAsync(entry.Id, "all", 0, "state", exception, failures, failuresPath, cancellationToken);
                continue;
            }
            await TryRunConditionAsync(entry.Id, "flat", 0, state, true, existing, outputPath, failures, failuresPath, cancellationToken);
            await TryRunConditionAsync(entry.Id, "hierarchical", 0, state, false, existing, outputPath, failures, failuresPath, cancellationToken);
            if (entry.AttachmentCount > 0)
            {
                var bodyOnly = await stateBuilder.BuildAsync(path, true, cancellationToken);
                await TryRunConditionAsync(entry.Id, "hierarchical-body-only", 0, bodyOnly, false, existing, outputPath, failures, failuresPath, cancellationToken);
            }
        }

        var primary = existing.Where(item => item.Repeat == 0 && item.Condition is "flat" or "hierarchical")
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => new
            {
                Id = group.Key,
                Confidence = group.Min(item => item.Confidence),
                Disagrees = group.Select(item => item.Family).Distinct(StringComparer.Ordinal).Count() > 1
            })
            .OrderByDescending(item => item.Disagrees)
            .ThenBy(item => item.Confidence)
            .Take(15)
            .ToArray();
        foreach (var candidate in primary)
        {
            var entry = cohort.Single(item => item.Id == candidate.Id);
            var state = await stateBuilder.BuildAsync(CorpusPath.Resolve(corpusRoot, entry.SourcePath), false, cancellationToken);
            for (var repeat = 1; repeat < 5; repeat++)
            {
                await TryRunConditionAsync(entry.Id, "flat", repeat, state, true, existing, outputPath, failures, failuresPath, cancellationToken);
                await TryRunConditionAsync(entry.Id, "hierarchical", repeat, state, false, existing, outputPath, failures, failuresPath, cancellationToken);
            }
        }
        EnsureSingleModel(existing);
        return existing;
    }

    private async Task TryRunConditionAsync(
        string id,
        string condition,
        int repeat,
        JevMailState state,
        bool flat,
        List<JevPrediction> results,
        string outputPath,
        List<EvaluationFailure> failures,
        string failuresPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunConditionAsync(id, condition, repeat, state, flat, results, outputPath, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(id, condition, repeat, "inference", exception, failures, failuresPath, cancellationToken);
        }
    }

    private static async Task RecordFailureAsync(
        string id,
        string condition,
        int repeat,
        string stage,
        Exception exception,
        List<EvaluationFailure> failures,
        string failuresPath,
        CancellationToken cancellationToken)
    {
        failures.Add(new(id, condition, repeat, stage, exception.GetType().Name, exception.Message, DateTimeOffset.UtcNow.ToString("O")));
        await EvaluationJson.WriteJsonLinesAsync(failuresPath, failures, cancellationToken);
    }

    private async Task RunConditionAsync(
        string id,
        string condition,
        int repeat,
        JevMailState state,
        bool flat,
        List<JevPrediction> results,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (results.Any(item => item.Id == id && item.Condition == condition && item.Repeat == repeat))
        {
            return;
        }
        var stateJson = JsonSerializer.Serialize(state, EvaluationJson.Options);
        var stateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateJson)));
        var first = await client.AskChoiceAsync(
            state,
            flat ? JevQuestions.FlatInstruction : JevQuestions.FamilyInstruction,
            flat ? JevQuestions.Flat() : JevQuestions.Families,
            cancellationToken);
        var detailedChoice = first.Answer.Choice;
        var family = flat ? FamilyFromDetailed(detailedChoice) : detailedChoice;
        string? subtype = flat ? SubtypeFromDetailed(detailedChoice) : null;
        IReadOnlyDictionary<string, double>? subtypeProbabilities = null;
        var inputTokens = first.InputTokens;
        var outputTokens = first.OutputTokens;
        var latency = first.LatencyMilliseconds;
        if (!flat && family != EvaluationTaxonomy.Abstain && EvaluationTaxonomy.Received[family].Length > 0)
        {
            var second = await client.AskChoiceAsync(
                state,
                $"Assuming the Received family is `{family}`, choose its supported subtype. Choose {EvaluationTaxonomy.Abstain} when none is supported.",
                JevQuestions.Subtypes(family),
                cancellationToken);
            if (!string.Equals(first.Model, second.Model, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The TypeSafe model alias changed within one hierarchical evaluation.");
            }
            subtype = second.Answer.Choice == EvaluationTaxonomy.Abstain ? null : second.Answer.Choice;
            subtypeProbabilities = second.Answer.Probabilities;
            inputTokens += second.InputTokens;
            outputTokens += second.OutputTokens;
            latency += second.LatencyMilliseconds;
        }
        var familyProbabilities = flat ? CollapseFamilies(first.Answer.Probabilities) : first.Answer.Probabilities;
        results.Add(new(
            id, condition, repeat, "jev-latest", first.Model, JevQuestions.PromptVersion, stateHash,
            family, subtype, first.Answer.Confidence, familyProbabilities, subtypeProbabilities,
            inputTokens, outputTokens, latency, DateTimeOffset.UtcNow.ToString("O")));
        EnsureSingleModel(results);
        await EvaluationJson.WriteJsonLinesAsync(outputPath, results, cancellationToken);
    }

    internal static string FamilyFromDetailed(string choice) =>
        choice == EvaluationTaxonomy.Abstain ? choice : choice.Split('/', 2)[0];

    internal static string? SubtypeFromDetailed(string choice) =>
        choice.Contains('/', StringComparison.Ordinal) ? choice.Split('/', 2)[1] : null;

    internal static IReadOnlyDictionary<string, double> CollapseFamilies(IReadOnlyDictionary<string, double> detailed) =>
        detailed.GroupBy(item => FamilyFromDetailed(item.Key), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value), StringComparer.Ordinal);

    internal static void EnsureSingleModel(IEnumerable<JevPrediction> predictions)
    {
        var models = predictions.Select(item => item.ActualModel).Distinct(StringComparer.Ordinal).ToArray();
        if (models.Length > 1)
        {
            throw new InvalidDataException($"A run cannot aggregate multiple returned model versions: {string.Join(", ", models)}.");
        }
    }
}
