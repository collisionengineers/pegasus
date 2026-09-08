using System.Text.RegularExpressions;
namespace Pegasus.Core.Intake;

#pragma warning disable CA1725

public sealed partial class AlsInstructionExtractionPolicy : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "als_instruction"; public const int Version = 1; public const string SupportedPrincipalCode = "ALS"; public const string DocumentProfileKeyValue = "als_instruction_document";
    static readonly InstructionFieldEngine.FieldDefinition[] Definitions = [new("Claimant name", ["Client name"], PartyRole: "claimant"), new("Vehicle owner", ["Vehicle owner"], IsRequired: false, PartyRole: "owner"), new("Claim reference", ["Our Reference"], PartyRole: "principal", ReferenceRole: "principal"), new("Vehicle registration", ["Client registration"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"), new("Vehicle make", ["Client vehicle make"], PartyRole: "claimant"), new("Vehicle model", ["Client vehicle model"], IsRequired: false, PartyRole: "claimant"), new("Incident date", ["Accident Date"], IsValidTyped: v => InstructionFieldEngine.ParseDate(v) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"), new("Instruction date", ["Header date"], IsValidTyped: v => InstructionFieldEngine.ParseDate(v) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"), new("Inspection address", ["Vehicle location"], IsRequired: false, PartyRole: "inspection-location"), new("Accident circumstances", ["Accident Circumstances"], IsRequired: false, PartyRole: "claimant"), new("VAT status", ["VAT Registered"], IsRequired: false, PartyRole: "claimant"), new("Repairer", ["Repairer"], IsRequired: false, PartyRole: "repairer"), new("Claimant address", ["Client address"], IsRequired: false, PartyRole: "claimant"), new("Vehicle class", ["Vehicle Category"], IsRequired: false, PartyRole: "vehicle-class"), new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: v => InstructionFieldEngine.ParseDate(v) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection")]; static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode; public string DocumentProfileKey => DocumentProfileKeyValue; public int DocumentProfileVersion => 1; public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole, ["Auto Logistic Solutions", "Vehicle Reg:", "Vehicle Model:"], ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]); public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(x => x.Name, x => new InstructionFieldRole(x.PartyRole, x.ReferenceRole), StringComparer.Ordinal);
    public InstructionExtractionResult Extract(IntakeSourceReadResult r, DateTimeOffset at, EstablishedPrincipalContext p) { if (r.Status != IntakeSourceReadStatus.Readable || r.IsIncomplete) throw new ArgumentException("ALS extraction requires complete readable content.", nameof(r)); if (p.PrincipalCode != SupportedPrincipalCode) throw new ArgumentException("The established principal is not ALS.", nameof(p)); var (f, m, e) = InstructionFieldEngine.ExtractFields(InstructionFields(r.Content).ToArray(), Definitions, Cache, at); var v = f.ToDictionary(x => x.Name, x => x.SuggestedValue, StringComparer.Ordinal); var d = new InstructionDraft("ALS", InstructionFieldEngine.TypedString(v["Claimant name"], 300), InstructionFieldEngine.TypedString(v["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(v["Vehicle registration"]), InstructionFieldEngine.TypedString(v["Vehicle make"], 100), InstructionFieldEngine.TypedString(v["Vehicle model"], 100), null, InstructionFieldEngine.TypedString(v["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(v["Incident date"]), InstructionFieldEngine.ParseDate(v["Instruction date"]), InstructionFieldEngine.TypedString(v["Inspection address"], 1000), InstructionFieldEngine.ParseDate(v["Inspection date"]), null, InstructionFieldEngine.TypedString(v["VAT status"], 100), null, null); return new(InstructionPolicyApplicability.Applicable, [.. e, new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal ALS was established by {p.PolicyKey} v{p.PolicyVersion}.")], f, d, m, Key, Version); }
    static IEnumerable<IntakeContentFragment> Fields(IntakeContentFragment f, bool includeVehicle) { var t = f.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'); var dear = Dear().Match(t); var sign = Sign().Match(t); if (!dear.Success || !sign.Success || sign.Index <= dear.Index) yield break; var h = t[..dear.Index]; var b = t[dear.Index..sign.Index]; foreach (Match x in Ref().Matches(h)) yield return L(f, "Our Reference", x.Groups["v"].Value); foreach (Match x in Date().Matches(h)) yield return L(f, "Header date", x.Groups["v"].Value); foreach (Match x in Accident().Matches(b)) yield return L(f, "Accident Date", x.Groups["v"].Value); foreach (Match x in Circ().Matches(b)) yield return L(f, "Accident Circumstances", x.Groups["v"].Value); foreach (Match x in Party().Matches(b)) { yield return L(f, "Client name", x.Groups["client"].Value); yield return L(f, "Client address", x.Groups["address"].Value); } foreach (Match x in Owner().Matches(b)) yield return L(f, "Vehicle owner", x.Groups["owner"].Value); foreach (Match x in Vat().Matches(b)) yield return L(f, "VAT Registered", x.Groups["v"].Value); if (includeVehicle) foreach (var vehicle in VehicleFields(f, b)) yield return vehicle; foreach (Match x in Location().Matches(b)) if (x.Groups["v"].Value.Trim(" .".ToCharArray()).Length > 0) yield return L(f, "Vehicle location", x.Groups["v"].Value); foreach (Match x in Repairer().Matches(b)) yield return L(f, "Repairer", x.Groups["v"].Value); }

    private static readonly (Regex Pattern, string Label)[] VehicleBindings =
    [
        (Vrm(), "Client registration"), (Make(), "Client vehicle make"),
        (Model(), "Client vehicle model"), (Category(), "Vehicle Category")
    ];

    private static IEnumerable<IntakeContentFragment> InstructionFields(IReadOnlyList<IntakeContentFragment> content)
    {
        foreach (var document in content.GroupBy(
            fragment => InstructionExtractionPolicySelector.DocumentIdentity(fragment.SourceLabel), StringComparer.Ordinal))
        {
            var cells = document.Where(fragment => fragment.Locator is
                { Kind: IntakeLocatorKind.TableCell, Table: { }, Row: { }, Column: { } }).ToArray();
            foreach (var fragment in document)
                foreach (var field in Fields(fragment, includeVehicle: cells.Length == 0))
                    yield return field;

            foreach (var table in cells.GroupBy(fragment => fragment.Locator!.Table))
            {
                var headers = table.Where(fragment => fragment.Locator!.Column == 1
                    && string.Equals(fragment.Text.Trim(), "Clients Vehicle", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (headers.Length != 1)
                    continue;
                var header = headers[0];
                var neighbours = table.Where(fragment => fragment.Locator!.Row == header.Locator!.Row
                    && fragment.Locator.Column == 2
                    && string.Equals(fragment.Text.Trim(), "Third Party Vehicle", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (neighbours.Length != 1)
                    continue;

                // Keep the actual header row so the shared field engine treats
                // the following label/value pairs as body rows, not headers.
                yield return header;
                yield return neighbours[0];
                foreach (var label in table.Where(fragment => fragment.Locator!.Column == 1
                    && fragment.Locator.Row > header.Locator!.Row))
                {
                    var values = table.Where(fragment => fragment.Locator!.Row == label.Locator!.Row
                        && fragment.Locator.Column == 2 && !string.IsNullOrWhiteSpace(fragment.Text)).ToArray();
                    if (values.Length != 1)
                        continue;
                    foreach (var binding in VehicleBindings)
                    {
                        if (!binding.Pattern.IsMatch($"{label.Text.Trim()} {values[0].Text}"))
                            continue;
                        yield return label with { Text = binding.Label };
                        yield return values[0];
                    }
                }
            }
        }
    }

    private static IEnumerable<IntakeContentFragment> VehicleFields(IntakeContentFragment fragment, string text)
    {
        foreach (var binding in VehicleBindings)
            foreach (Match match in binding.Pattern.Matches(text))
                yield return L(fragment, binding.Label, match.Groups["v"].Value);
    }

    static IntakeContentFragment L(IntakeContentFragment f, string l, string v) => f with { Text = $"{l}: {Ws().Replace(v, " ").Trim()}" };
    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex Dear();
    [GeneratedRegex(@"(?im)^\s*Auto\s+Logistic\s+Solutions\s+Ltd\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex Sign();
    [GeneratedRegex(@"(?im)^\s*Our\s+Reference\s*:[ \t]*(?<v>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Ref();
    [GeneratedRegex(@"(?im)^\s*(?<v>\d{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]+\s+\d{4})\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex Date();
    [GeneratedRegex(@"(?im)^\s*Accident\s+Date\s*:[ \t]*(?<v>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Accident();
    [GeneratedRegex(@"(?ims)Accident\s+Circumstances\s*:[ \t]*(?<v>.+?)(?=\n\s*Client\s+Details)", RegexOptions.CultureInvariant, 100)] private static partial Regex Circ();
    // The name paragraph follows the paired Client/Owner header in binary
    // DOC; the owner's Name remains across a single tab after the client Tel.
    [GeneratedRegex(@"(?ims)Client\s+Details.*?(?:\n|\t{2})[ \t]*Name\s*:[ \t]*(?<client>[^\t\r\n]+).*?\n\s*Address\s*:[ \t]*(?<address>[^\t\r\n]+).*?(?:\tName\s*:[ \t]*(?<owner>[^\t\r\n]+))?\s*\n.*?VAT\s+Registered", RegexOptions.CultureInvariant, 100)] private static partial Regex Party();
    [GeneratedRegex(@"(?im)^\s*Tel\s*:[^\r\n]*\tName\s*:[ \t]*(?<owner>[^\t\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Owner();
    [GeneratedRegex(@"(?im)^\s*VAT\s+Registered\s*:[ \t]*(?<v>yes|no)\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Vat();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Reg\s*:[ \t]*(?<v>[^\t\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Vrm();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Make\s*:[ \t]*(?<v>[^\t\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Make();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Model\s*:[ \t]*(?<v>[^\t\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Model();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Category\s*:[ \t]*(?<v>[^\t\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Category();
    [GeneratedRegex(@"(?im)vehicle\s+is\s+currently[^\r\n]*?found\s+at[ \t]*(?<v>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex Location();
    [GeneratedRegex(@"(?im)^\s*(?<v>[^\r\n]+?)\s+of\s+[^\r\n]+$", RegexOptions.CultureInvariant, 100)] private static partial Regex Repairer();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex Ws();
#pragma warning restore CA1725
}
