using System.Globalization;

namespace Pegasus.Core.Intake;

public sealed class QdosInstructionExtractionPolicy(
    IIntakeTriageMatcher? triageMatcher = null) : IInstructionExtractionPolicy
{
    public const string Key = "qdos_instruction";
    public const int Version = 2;
    public const string SupportedPrincipalCode = "QDOS";
    private readonly IIntakeTriageMatcher triageMatcher =
        triageMatcher ?? new NoAcceptedIntakeTriageMatcher();

    public string PrincipalCode => SupportedPrincipalCode;

    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant"]),
        new("Claim number", ["Claim Number", "Claim No", "Claim Reference"]),
        new(
            "Vehicle registration",
            [
                "Vehicle Registration", "Registration Number", "Registration No",
                "Vehicle Reg No", "Vehicle Reg", "Registration", "Reg No", "VRM"
            ],
            IsValidTyped: InstructionFieldEngine.IsCurrentFormatRegistration),
        new("Vehicle make", ["Vehicle Make", "Make"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel),
        new("Vehicle model", ["Vehicle Model", "Model"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"],
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new("Date of incident", ["Date of Incident", "Incident Date", "Accident Date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null),
        new("Instruction date", ["Instruction Date", "Date of Instruction"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"]),
        new(
            "Inspection date",
            ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null)
    ];

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc,
        EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
        {
            throw new ArgumentException(
                "The QDOS extraction policy accepts only fully readable, complete reader results.",
                nameof(readResult));
        }
        if (!string.Equals(
                principalContext.PrincipalCode,
                SupportedPrincipalCode,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The established principal is not supported by the QDOS extraction policy.",
                nameof(principalContext));
        }

        var evidence = new List<IntakeEvidence>
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal QDOS was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        var (fields, missingFields, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            readResult.Content,
            FieldDefinitions,
            processedAtUtc);
        evidence.AddRange(fieldEvidence);
        var draft = CreateInstructionDraft(fields, principalContext.PrincipalCode);
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
                "A QDOS draft was extracted from readable content; additional scanned PDF content still requires review."));
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

    private static InstructionDraft CreateInstructionDraft(
        IReadOnlyList<InstructionReviewField> fields,
        string principalCode)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            principalCode,
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

    private static void ValidateTriageMatch(IntakeTriageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.MatcherKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(match.MatcherVersion);
    }

}
