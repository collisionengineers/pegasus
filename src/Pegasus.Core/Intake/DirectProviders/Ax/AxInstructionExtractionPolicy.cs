namespace Pegasus.Core.Intake;

/// <summary>Reads AX engineer instructions without treating repairer or deadline facts as inspection facts.</summary>
public sealed class AxInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "ax_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "AX";
    public const string DocumentProfileKeyValue = "ax_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private const string DeadlineField = "Report deadline";
    private const string RepairerAddressField = "Repairer address";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Name"], PartyRole: "claimant"),
        new("Claim reference", ["AX Reference"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["VRM"], IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make", ["Vehicle"], AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Vehicle model", ["Model"], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel, PartyRole: "claimant"),
        new("Incident date", ["Accident Date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Instruction date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Inspection address", ["Inspection Location"], IsRequired: false, PartyRole: "instruction"),
        new("Accident circumstances", ["Accident Circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", ["VAT Registered"], IsRequired: false, PartyRole: "claimant"),
        new("Repairer name", ["Repairer name"], IsRequired: false, PartyRole: "repairer"),
        new(RepairerAddressField, [RepairerAddressField], IsRequired: false, PartyRole: "repairer"),
        new(DeadlineField, [DeadlineField], IsRequired: false, PartyRole: "deadline")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["AX Reference", "VRM:", "Vehicle:"], ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        definition => definition.Name,
        definition => new InstructionFieldRole(definition.PartyRole, definition.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc,
        EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("AX extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not AX.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(Scope).ToArray();
        var (fields, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(scoped, Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100), null, null,
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000), null, null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(fieldEvidence)
        {
            new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal", $"Principal AX was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    private static IEnumerable<IntakeContentFragment> Scope(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var client = text.IndexOf("Client Details", StringComparison.OrdinalIgnoreCase);
        if (client < 0) yield break;
        var bodyshop = text.IndexOf("Bodyshop Details", client, StringComparison.OrdinalIgnoreCase);
        var thirdParty = text.IndexOf("Third Party Details", client, StringComparison.OrdinalIgnoreCase);
        var clientEnd = bodyshop >= 0 ? bodyshop : thirdParty >= 0 ? thirdParty : text.Length;
        var header = text[..client];
        var headerDate = header.Split('\n').Select(line => line.Trim()).FirstOrDefault(line =>
            InstructionFieldEngine.ParseDate(line) is not null);
        var deadline = header.Split('\n').FirstOrDefault(line => line.Contains("Report Due on", StringComparison.OrdinalIgnoreCase));
        yield return fragment with { Text = text[client..clientEnd] };
        yield return fragment with { Text = $"AX Reference: {ValueAfter(header, "AX Reference")}" };
        if (headerDate is not null) yield return fragment with { Text = $"Instruction date: {headerDate}" };
        if (deadline is not null && DateToken(deadline) is { } deadlineDate)
            yield return fragment with { Text = $"{DeadlineField}: {deadlineDate}" };
        if (bodyshop >= 0)
        {
            var end = thirdParty >= 0 ? thirdParty : text.Length;
            var block = text[bodyshop..end];
            yield return fragment with { Text = $"Repairer name: {ValueAfter(block, "Name")}" };
            yield return fragment with { Text = $"{RepairerAddressField}: {ValueAfter(block, "Address")}" };
        }
    }

    private static string ValueAfter(string text, string label)
    {
        var index = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;
        var value = text[(index + label.Length)..].TrimStart(' ', '\t', ':');
        return value.Split('\n')[0].Trim();
    }

    private static string? DateToken(string text) => text
        .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
        .Select(token => token.Trim(':'))
        .FirstOrDefault(token => InstructionFieldEngine.ParseDate(token) is not null);
}
