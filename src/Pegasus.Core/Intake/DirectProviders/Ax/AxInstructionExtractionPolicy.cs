using System.Text.RegularExpressions;

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
    private const string RepairerTelephoneField = "Repairer telephone";

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
        new(
            "Accident circumstances",
            ["Accident circumstances"],
            IsRequired: false,
            PartyRole: "claimant",
            PrefersLatestFragment: true),
        new("VAT status", ["VAT Registered"], IsRequired: false, PartyRole: "claimant"),
        new("Repairer name", ["Repairer name"], IsRequired: false, PartyRole: "repairer"),
        new(RepairerAddressField, [RepairerAddressField], IsRequired: false, PartyRole: "repairer"),
        new(RepairerTelephoneField, [RepairerTelephoneField], IsRequired: false, PartyRole: "repairer"),
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
        var bodyshop = text.IndexOf("Bodyshop Details", StringComparison.OrdinalIgnoreCase);
        var thirdParty = text.IndexOf("Third Party Details", StringComparison.OrdinalIgnoreCase);
        var clientEnd = NextSection(text.Length, client, bodyshop, thirdParty);
        var header = text[..client];
        var headerDate = header.Split('\n').Select(line => line.Trim()).FirstOrDefault(line =>
            InstructionFieldEngine.ParseDate(line) is not null);
        var deadline = header.Split('\n').FirstOrDefault(line => line.Contains("Report Due on", StringComparison.OrdinalIgnoreCase));
        yield return fragment with { Text = text[client..clientEnd] };
        yield return fragment with { Text = $"AX Reference: {FirstColumnValueAfter(header, "AX Reference")}" };
        if (headerDate is not null) yield return fragment with { Text = $"Instruction date: {headerDate}" };
        if (deadline is not null && DateToken(deadline) is { } deadlineDate)
            yield return fragment with { Text = $"{DeadlineField}: {deadlineDate}" };
        if (Circumstances(text[client..clientEnd]) is { } circumstances)
            yield return fragment with { Text = $"Accident circumstances: {circumstances}" };
        if (bodyshop >= 0)
        {
            var end = NextSection(text.Length, bodyshop, client, thirdParty);
            var block = text[bodyshop..end];
            yield return fragment with { Text = $"Repairer name: {FirstColumnValueAfter(block, "Name")}" };
            var address = Address(block);
            if (address is not null)
                yield return fragment with { Text = $"{RepairerAddressField}: {address}" };
            var telephone = FirstColumnValueAfter(block, "Telephone");
            if (telephone.Length > 0)
                yield return fragment with { Text = $"{RepairerTelephoneField}: {telephone}" };
        }
    }

    private static int NextSection(int fallback, int after, params int[] candidates) =>
        candidates.Where(candidate => candidate > after).DefaultIfEmpty(fallback).Min();

    private static string FirstColumnValueAfter(string text, string label)
    {
        var index = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;
        var value = text[(index + label.Length)..].TrimStart(' ', '\t', ':');
        return Regex.Split(value.Split('\n')[0], @"\s{2,}", RegexOptions.CultureInvariant)[0].Trim();
    }

    private static string? Circumstances(string clientBlock)
    {
        var lines = clientBlock.Split('\n', StringSplitOptions.TrimEntries);
        var start = Array.FindIndex(lines, line => line.StartsWith("Accident", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("Accident Date", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return null;
        var values = new List<string>();
        for (var index = start; index < lines.Length; index++)
        {
            var line = lines[index];
            if (index > start && (line.StartsWith("Pre Existing", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Damage", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("VAT Registered", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Bodyshop Details", StringComparison.OrdinalIgnoreCase))) break;
            line = Regex.Replace(line, @"^(?:Accident\s*)?(?:Circumstances\s*:?)?\s*", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            if (line.Length > 0) values.Add(line);
        }
        return values.Count == 0 ? null : string.Join(' ', values);
    }

    private static string? Address(string bodyshopBlock)
    {
        var lines = bodyshopBlock.Split('\n', StringSplitOptions.TrimEntries);
        var start = Array.FindIndex(lines, line => line.StartsWith("Address", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return null;
        var values = new List<string>();
        var first = FirstColumnValueAfter(lines[start], "Address");
        if (first.Length > 0) values.Add(first);
        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("Telephone", StringComparison.OrdinalIgnoreCase)) break;
            if (line.StartsWith("Postcode", StringComparison.OrdinalIgnoreCase))
            {
                var postcode = FirstColumnValueAfter(line, "Postcode");
                if (postcode.Length > 0) values.Add(postcode);
                continue;
            }
            if (line.Length > 0 && !line.EndsWith(':')) values.Add(line);
        }
        return values.Count == 0 ? null : string.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? DateToken(string text) => text
        .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
        .Select(token => token.Trim(':'))
        .FirstOrDefault(token => InstructionFieldEngine.ParseDate(token) is not null);
}
