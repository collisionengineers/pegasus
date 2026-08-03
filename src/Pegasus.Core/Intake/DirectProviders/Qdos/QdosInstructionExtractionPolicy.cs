using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class QdosInstructionExtractionPolicy(
    IIntakeTriageMatcher? triageMatcher = null) : IInstructionExtractionPolicy
{
    public const string Key = "qdos_instruction";
    public const int Version = 1;
    private const string PrincipalCode = "QDOS";
    private static readonly QdosMailRoutePolicy RoutePolicy = new();
    private readonly IIntakeTriageMatcher triageMatcher =
        triageMatcher ?? new NoAcceptedIntakeTriageMatcher();

    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant"]),
        new("Claim number", ["Claim Number", "Claim No", "Claim Reference"]),
        new("Vehicle registration", ["Vehicle Registration", "Registration", "VRM"]),
        new("Vehicle make", ["Vehicle Make", "Make"]),
        new("Vehicle model", ["Vehicle Model", "Model"]),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"]),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new("Date of incident", ["Date of Incident", "Incident Date", "Accident Date"]),
        new("Instruction date", ["Instruction Date", "Date of Instruction"]),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"]),
        new("Inspection date", ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"], IsRequired: false)
    ];

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc)
    {
        var route = RoutePolicy.Evaluate(readResult);
        var evidence = new List<IntakeEvidence>();
        var confirmingFragments = new List<IntakeContentFragment>();
        foreach (var fragment in readResult.Content)
        {
            var labelsFound = FieldDefinitions.Count(definition =>
                definition.Labels.Any(label => InstructionFieldEngine.ContainsLabel(fragment.Text, label)));
            var hasQdos = QdosMarkerRegex().IsMatch(fragment.Text);

            if (hasQdos)
            {
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-content-marker",
                    $"QDOS was identified in {fragment.SourceLabel}."));
            }

            if (hasQdos && labelsFound >= 2)
            {
                confirmingFragments.Add(fragment);
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "instruction-structure",
                    $"{fragment.SourceLabel} contains QDOS and {labelsFound} instruction field labels."));
            }
        }

        AddTransportEvidence(
            readResult.TransportEvidence,
            route,
            confirmingFragments.Count > 0,
            evidence);

        if (confirmingFragments.Count > 0)
        {
            var (fields, missingFields, fieldEvidence) = InstructionFieldEngine.ExtractFields(
                readResult.Content,
                FieldDefinitions,
                processedAtUtc);
            evidence.AddRange(fieldEvidence);
            var draft = CreateInstructionDraft(fields);
            var triageMatches = triageMatcher.Match(readResult, draft);
            ArgumentNullException.ThrowIfNull(triageMatches);
            foreach (var match in triageMatches)
            {
                ValidateTriageMatch(match);
                evidence.Add(new(
                    match.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.AcceptedTriageMatch,
                    match.Signal.Trim(),
                    match.Detail.Trim(),
                    match.MatcherKey.Trim(),
                    match.MatcherVersion));
            }
            if (readResult.RequiresOcr)
            {
                evidence.Add(new(
                    IntakeEvidenceSource.PdfContent,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.Information,
                    "additional-scanned-content",
                    "A QDOS-shaped draft was extracted from readable content; additional scanned PDF content still requires review."));
            }

            return new(
                InstructionPolicyApplicability.Applicable,
                evidence,
                fields,
                draft,
                missingFields,
                Key,
                Version);
        }

        return new(
            evidence.Count == 0 && !readResult.RequiresOcr
                ? InstructionPolicyApplicability.NotApplicable
                : InstructionPolicyApplicability.Indeterminate,
            evidence,
            [],
            null,
            [],
            Key,
            Version);
    }

    private static InstructionDraft CreateInstructionDraft(IReadOnlyList<InstructionReviewField> fields)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            PrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim number"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Date of incident"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]));
    }

    private static void AddTransportEvidence(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        MailRouteEvaluationResult route,
        bool contentConfirmed,
        List<IntakeEvidence> evidence)
    {
        if (route.Disposition == MailRouteDisposition.Accepted)
        {
            evidence.Add(new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "qdos-direct-mail-route",
                "The effective sender uses the accepted direct QDOS domain."));
        }

        foreach (var item in transportEvidence)
        {
            if (QdosMarkerRegex().IsMatch(item.Value))
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-transport-marker",
                    $"{DisplaySource(item.Source)} contains a QDOS marker."));
            }
            else if (contentConfirmed && item.Source == IntakeEvidenceSource.Sender)
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.ContradictsTransport,
                    "forwarded-sender",
                    "The retained transport sender is not the proved effective original sender."));
            }
        }
    }

    private static void ValidateTriageMatch(IntakeTriageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.MatcherKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(match.MatcherVersion);
    }

    private static string DisplaySource(IntakeEvidenceSource source) => source switch
    {
        IntakeEvidenceSource.Sender => "Sender",
        IntakeEvidenceSource.Subject => "Subject",
        IntakeEvidenceSource.FileName => "File name",
        IntakeEvidenceSource.MimeType => "File type",
        _ => "Transport metadata"
    };

    [GeneratedRegex(@"\bQDOS\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QdosMarkerRegex();
}
