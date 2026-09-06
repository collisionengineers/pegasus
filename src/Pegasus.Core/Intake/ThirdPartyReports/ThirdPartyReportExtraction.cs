using System.Globalization;
using System.Text.RegularExpressions;
using F = Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyReportFields;
using K = Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyValueKind;
using R = Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyEstimateRoles;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// The canonical field names carried by every third-party source row. One name
/// per concept: the printed amount role lives in the candidate's reference
/// role, never in a second copy of the field name.
/// </summary>
public static class ThirdPartyReportFields
{
    public const string Issuer = "identity.issuer";
    public const string EngineerName = "identity.engineer.name";
    public const string EngineerQualifications = "identity.engineer.qualifications";
    public const string ReportReference = "identity.report.reference";
    public const string ClaimReference = "identity.claim.reference";
    public const string ReportDate = "identity.report.date";
    public const string Revision = "identity.revision";
    public const string Amendment = "identity.amendment";

    public const string Registration = "vehicle.registration";
    public const string Make = "vehicle.make";
    public const string Model = "vehicle.model";
    public const string Variant = "vehicle.variant";
    public const string Vin = "vehicle.vin";
    public const string Mileage = "vehicle.mileage";
    public const string MileageUnit = "vehicle.mileage.unit";

    public const string Claimant = "parties.claimant";
    public const string Repairer = "parties.repairer";
    public const string RepairerAddress = "parties.repairer.address";
    public const string VehicleLocation = "parties.vehicle.location";
    public const string AccidentDate = "parties.accident.date";
    public const string InspectionDate = "parties.inspection.date";

    public const string Outcome = "damage.outcome";
    public const string Repairability = "damage.repairability";
    public const string Roadworthiness = "damage.roadworthiness";
    public const string OutcomeReason = "damage.outcome.reason";
    public const string Severity = "damage.severity";
    public const string Narrative = "damage.narrative";
    public const string PriorDamage = "damage.prior";
    public const string Tyres = "damage.tyres";
    public const string Restraints = "damage.restraints";
    public const string Airbags = "damage.airbags";
    public const string DamageZone = "damage.zone";

    public const string LabourHours = "estimate.labour.hours";
    public const string LabourRate = "estimate.labour.rate";
    public const string LabourAmount = "estimate.labour.amount";
    public const string PaintMaterials = "estimate.paint.materials";
    public const string Parts = "estimate.parts";
    public const string SpecialistCharges = "estimate.specialist.charges";
    public const string AdditionalCharges = "estimate.additional.charges";
    public const string Discounts = "estimate.discounts";
    public const string Net = "estimate.net";
    public const string VatRate = "estimate.vat.rate";
    public const string VatAmount = "estimate.vat.amount";
    public const string Gross = "estimate.gross";

    public const string ValuationGuide = "valuation.guide";
    public const string ValuationGuideDate = "valuation.guide.date";
    public const string Trade = "valuation.trade";
    public const string Retail = "valuation.retail";
    public const string Mid = "valuation.mid";
    public const string PreAccidentValue = "valuation.pav";
    public const string MileageAdjustment = "valuation.mileage.adjustment";
    public const string ConditionAdjustment = "valuation.condition.adjustment";

    /// <summary>
    /// A printed value adjustment whose label is neither mileage nor condition
    /// (Montgomery prints "Urban edition adjustment"). The frozen C-B02
    /// projection types only the mileage and condition slots, so this one stays
    /// a source row: its printed label is kept in the raw text and the
    /// reconciliation reads the row rather than inventing a typed slot for it.
    /// </summary>
    public const string ValuationAdjustment = "valuation.adjustment";
    public const string FinalValue = "valuation.final";
    public const string SalvageCategory = "valuation.salvage.category";
    public const string SalvageValue = "valuation.salvage.value";
    public const string SalvageBid = "valuation.salvage.bid";
    public const string Excess = "valuation.excess";
    public const string Reserve = "valuation.reserve";
    public const string CashInLieu = "valuation.cash.in.lieu";
    public const string Deduction = "valuation.deduction";

    public const string MinimumRepairDays = "declaration.repair.days.minimum";
    public const string MaximumRepairDays = "declaration.repair.days.maximum";
    public const string RequestedInspectionMethod = "declaration.inspection.method.requested";
    public const string ObservedInspectionMethod = "declaration.inspection.method.observed";
    public const string Comments = "declaration.comments";
    public const string SupplementReason = "declaration.supplement.reason";
    public const string Declaration = "declaration.declaration";
    public const string Signatory = "declaration.signatory";

    /// <summary>
    /// A page whose text the reader could not take. It is a locator, not a
    /// value: the row is always Missing, and it exists so a scan-only page is
    /// visible as unread rather than silently absent.
    /// </summary>
    public const string PageRequiresHumanVerification = "source.page.requires-human-verification";

    public const string Photograph = "media.photograph";

    /// <summary>
    /// The namespace every reconciliation finding is recorded under. A finding
    /// is not a printed value, so it never shares a field name with one: the
    /// finding code follows the prefix, which is what tells a reader — and the
    /// Received screen — that the row states an observation ABOUT the printed
    /// values rather than one of them.
    /// </summary>
    public const string FindingPrefix = "finding.";

    /// <summary>The field name one finding code is recorded under.</summary>
    public static string Finding(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return FindingPrefix + code;
    }

    /// <summary>
    /// Whether a persisted row states a finding rather than a printed value.
    /// A surface that lists printed values filters on this; one that shows the
    /// document's contradictions selects on it.
    /// </summary>
    public static bool IsFinding(string field) =>
        field is not null && field.StartsWith(FindingPrefix, StringComparison.Ordinal);
}

/// <summary>The printed amount roles, as they appear on a persisted source row.</summary>
public static class ThirdPartyEstimateRoles
{
    public const string Initial = "initial";
    public const string Claimed = "claimed";
    public const string Assessed = "assessed";
    public const string Agreed = "agreed";
    public const string Revised = "revised";
    public const string Supplement = "supplement";
    public const string ContractRepair = "contract-repair";

    public static string Code(ThirdPartyEstimateRole role) => role switch
    {
        ThirdPartyEstimateRole.Initial => Initial,
        ThirdPartyEstimateRole.Claimed => Claimed,
        ThirdPartyEstimateRole.Assessed => Assessed,
        ThirdPartyEstimateRole.Agreed => Agreed,
        ThirdPartyEstimateRole.Revised => Revised,
        ThirdPartyEstimateRole.Supplement => Supplement,
        ThirdPartyEstimateRole.ContractRepair => ContractRepair,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown printed amount role.")
    };
}

/// <summary>
/// What one source candidate is read as. The kind decides normalization only;
/// the raw printed text is always preserved beside it.
/// </summary>
internal enum ThirdPartyValueKind
{
    Text,
    Reference,
    Money,
    Number,
    Date,
    Registration,
    Mileage
}

/// <summary>
/// One bounded label rule. <c>Pattern</c> must capture the printed value in a
/// group named <c>v</c> (or, for a section rule, match the heading line);
/// <c>CoLabel</c> restricts the rule to pages that also carry another label,
/// which is how an embedded fee invoice is kept out of the repair totals
/// without any positional reasoning.
/// </summary>
/// <param name="Pattern">
/// May contain the token <c>#END#</c>, which compiles to the end of a printed
/// cell: a run of two or more spaces, the end of the line, or the next printed
/// label. That is what makes a free-text cell readable whether the PDF text
/// engine preserves the column padding or collapses it to one space — the two
/// engines behind the reference pack and the production reader differ, and a
/// rule that depended on the padding would silently read nothing.
/// </param>
/// <param name="Until">
/// The labels that may follow this value on the same printed row, overriding
/// the family's shared label list in <c>#END#</c>.
/// </param>
/// <param name="RawWholeMatch">
/// Keeps the whole matched text as the raw value instead of the captured
/// group, so a printed label the projection has no typed slot for is still
/// preserved beside its number.
/// </param>
internal sealed record ThirdPartyFieldRule(
    string Field,
    ThirdPartyValueKind Kind,
    string Pattern,
    string ReferenceRole = "",
    string PartyRole = "",
    string? CoLabel = null,
    string? Unit = null,
    bool Multiple = false,
    bool Section = false,
    SourceCandidateDisposition? Force = null,
    string? Until = null,
    bool RawWholeMatch = false);

/// <summary>
/// One family's bounded rules plus the printed labels that end a cell in its
/// layout. The labels are evidence from the reference corpus, never a guess.
/// </summary>
internal sealed record ThirdPartyFamilyRules(
    string LabelBoundary,
    IReadOnlyList<ThirdPartyFieldRule> Rules);

/// <summary>
/// The typed third-party report candidate plus every source row behind it.
/// <see cref="Candidates"/> carries one row per observed value — including both
/// halves of a conflict and an explicit Missing row for every field the
/// selected family declares — and is what the intake store persists.
/// </summary>
public sealed record ThirdPartyReportExtractionResult(
    ThirdPartyReportSelection Selection,
    ThirdPartyReportCandidate? Candidate,
    IReadOnlyList<SourceFieldCandidate> Candidates,
    IReadOnlyList<ThirdPartyReportFinding> Findings);

/// <summary>
/// Reads a heading-delimited section out of one page without a backtracking
/// regex: from the heading line to the next heading, blank break, or footer.
/// </summary>
internal static class ThirdPartySections
{
    private const int MaximumBodyLines = 12;

    private static readonly Regex Heading = ThirdPartyRegex.CreateMultiline(
        @"^[A-Z][A-Z0-9 '/&()-]{4,39}$");

    private static readonly Regex Footer = ThirdPartyRegex.CreateMultiline(
        @"^\s*(?:Email|Tel|Telephone|VAT|Web|Page)\b\s*:");

    public static IEnumerable<string> Read(string text, Regex heading)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!heading.IsMatch(lines[index]))
            {
                continue;
            }

            var body = new List<string>();
            for (var next = index + 1; next < lines.Length && body.Count < MaximumBodyLines; next++)
            {
                var line = lines[next];
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    if (body.Count > 0)
                    {
                        break;
                    }

                    continue;
                }

                if (Heading.IsMatch(trimmed) || Footer.IsMatch(line))
                {
                    break;
                }

                body.Add(trimmed);
            }

            if (body.Count > 0)
            {
                yield return string.Join(' ', body);
            }
        }
    }
}

/// <summary>
/// Builds a <see cref="ThirdPartyReportCandidate"/> from the selected family's
/// bounded label rules (INTK-031). Every value keeps its printed text, page
/// locator, unit and role; nothing is taken by position, and no field is filled
/// from another document. Arithmetic never edits a source value — it becomes a
/// finding (see <see cref="ThirdPartyReportValidation"/>).
/// </summary>
public static class ThirdPartyReportExtraction
{
    /// <summary>Versioned with the rule tables; recorded on every candidate.</summary>
    public const string ProfileVersion = "third-party-report-extraction/1";

    // Printed money is not always two decimals — Laird prints "£1686.7" — and
    // a two-decimal-only pattern silently dropped the tenth, which is exactly
    // the kind of quiet edit to a source value the invariants forbid.
    private const string Money = @"£\s*(?<v>-?[\d,]+(?:\.\d{1,2})?)";
    private const string BareMoney = @"(?<v>-?[\d,]+\.\d{2})";
    private const string PoundSign = "£";
    // The same printed heading the Laird signature is gated on, so the
    // document's own words have one owner rather than two spellings.
    private const string SupplementaryHeading =
        ThirdPartyReportProfiles.SupplementaryReportTitle;

    /// <summary>
    /// The Connexus, Exclusive EREHR and EVA bodyshop reports share one printed
    /// narrative layout; they are separate families because their issuer and
    /// reference roles differ, not because the layout does. One rule list, so a
    /// label lives in exactly one place.
    /// </summary>
    private static readonly ThirdPartyFieldRule[] NarrativeRules =
    [
        new(F.ReportDate, K.Date, @"Date:\s*(?<v>\d{1,2}/\d{1,2}/\d{4})", CoLabel: @"Our\s+Ref"),
        new(F.ReportReference, K.Reference, @"Our\s+Ref:\s*(?<v>\d{4,})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Reference, @"Your\s+Ref:\s*(?<v>[A-Za-z]{0,4}\d[A-Za-z0-9/]*)", ReferenceRole: "your-ref"),
        new(F.ClaimReference, K.Reference, @"Claim\s+No:\s*(?<v>[A-Za-z]{0,4}\d[A-Za-z0-9/]*)", ReferenceRole: "your-ref"),
        new(F.Revision, K.Text, @"REPORT\s*-\s*(?<v>Amended\s+Report|Supplementary\s+Report)"),
        new(F.EngineerName, K.Text,
            @"^[ \t]*(?<v>[A-Z][A-Za-z'\-]+(?:[ \t]+[A-Z][A-Za-z'\-]+)+?)(?:[ \t]+[A-Z]{2,6})*[ \t]*\n[ \t]*(?:Connexus|Exclusive)\s*Vehicle\s*Assessors"),
        // The name repetition is lazy so the qualifications keep every printed
        // post-nominal: a greedy name would swallow "AQP CAE" and leave only
        // the last one behind.
        new(F.EngineerQualifications, K.Text,
            @"^[ \t]*[A-Z][A-Za-z'\-]+(?:[ \t]+[A-Z][A-Za-z'\-]+)+?[ \t]+(?<v>[A-Z]{2,6}(?:[ \t]+[A-Z]{2,6})*)[ \t]*\n[ \t]*(?:Connexus|Exclusive)\s*Vehicle\s*Assessors"),
        new(F.Claimant, K.Text, @"^[ \t]*Client(?:/Insured)?:[ \t]*(?<v>[^\n]{2,80}?)[ \t]*$", PartyRole: "claimant"),

        // The narrative prints one combined vehicle description and never
        // separates make from model, so the printed text is recorded as
        // Ambiguous rather than split into two facts the source does not make.
        new(F.Model, K.Text, @"Vehicle:[ \t]*(?<v>[A-Z0-9][^\n]*?)#END#",
            Until: "Colour", Force: SourceCandidateDisposition.Ambiguous),

        new(F.Registration, K.Registration, @"Reg\s+No:[ \t]*(?<v>[A-Z0-9]{1,4} ?[A-Z0-9]{1,4})"),
        new(F.Vin, K.Text, @"Vin\s+No:[ \t]*(?<v>[A-HJ-NPR-Z0-9]{11,17})"),
        new(F.Mileage, K.Mileage, @"Speedo:[ \t]*(?<v>[\d,]+)", Unit: "miles"),
        new(F.MileageUnit, K.Text, @"Speedo:[ \t]*[\d,]*[ \t]*(?<v>Miles|Km)\b"),
        new(F.AccidentDate, K.Date, @"Incident:[ \t]*(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Severity, K.Text, @"Damage:[ \t]*(?<v>Light|Moderate|Heavy|Medium|Severe)\b"),
        new(F.DamageZone, K.Text,
            @"Damage:[ \t]*(?:Light|Moderate|Heavy|Medium|Severe)[ \t]+(?<v>[A-Za-z/ ]{3,40}?)#END#",
            Until: "Incident", Multiple: true),
        new(F.Roadworthiness, K.Text, @"Roadworthy:[ \t]*(?<v>Yes|No)\b"),
        new(F.OutcomeReason, K.Text,
            @"not\s+roadworthy\s+at\s+the\s+time\s+of\s+(?:our|my)\s+inspection\s+as\s+a\s+result\s+of\s+the\s+damage\s+sustained\s+due\s+to\s+(?<v>[^\n.]{2,120})\."),
        new(F.Narrative, K.Text, @"^\s*NATURE\s+OF\s+(?:INCIDENT|DAMAGE)\s*$", Section: true),
        new(F.Comments, K.Text, @"^\s*ENGINEER'S\s+COMMENTS\s*$", Section: true),
        new(F.ObservedInspectionMethod, K.Text, @"^\s*(?<v>DESKTOP\s+ASSESSMENT)\s*$"),
        new(F.Repairer, K.Text, @"The\s+repairers,\s*(?<v>[^,\n]{3,80}),", PartyRole: "repairer"),
        new(F.Repairer, K.Text, @"estimate\s+has\s+been\s+obtained\s+from\s*(?<v>[^,\n]{3,80}),", PartyRole: "repairer"),
        new(F.Declaration, K.Text,
            @"(?<v>In\s+preparing\s+this\s+report\s+I\s+confirm\s+that\s+I\s+understand\s+my\s+overriding\s+duty\s+to\s+the\s+court[^.]{0,200}\.)"),

        new(F.Gross, K.Money, @"Repair\s+Cost:\s*" + Money + @"\s*inc\s*VAT", ReferenceRole: R.Agreed),
        new(F.Net, K.Money, @"Repair\s+Cost:\s*" + Money + @"\s*exc\s*VAT", ReferenceRole: R.Agreed),
        new(F.LabourAmount, K.Money, @"in\s+the\s+sum\s+of\s*" + Money, ReferenceRole: R.Initial),
        new(F.PaintMaterials, K.Money, @"plus\s*" + Money + @"\s*for\s+paint\s+and\s+materials", ReferenceRole: R.Initial),
        new(F.SpecialistCharges, K.Money, @"plus\s*" + Money + @"\s*for\s+specialist/sundry\s+charges", ReferenceRole: R.Initial),
        new(F.LabourAmount, K.Money,
            @"(?:agreed\s+an\s+amended\s+labour\s+figure\s+of|agreed\s+a\s+labour\s+figure\s+of|consider\s+a\s+labour\s+charge\s+of)\s*" + Money,
            ReferenceRole: R.Agreed),
        new(F.LabourHours, K.Number, @"labour\s+charge\s+is\s+based\s+on\s*(?<v>[\d.,]+)\s*hours",
            ReferenceRole: R.Agreed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"hours\s+at\s+a\s+rate\s+of\s*" + Money, ReferenceRole: R.Agreed, Unit: "per hour"),
        new(F.PaintMaterials, K.Money, @"cost\s+of\s+paint\s+and\s+materials\s+(?:will|should)\s+be\s+limited\s+to\s*" + Money,
            ReferenceRole: R.Agreed),
        new(F.Parts, K.Money, @"replacement\s+parts\s+will\s+be\s+approximately\s*" + Money, ReferenceRole: R.Agreed),
        new(F.SpecialistCharges, K.Money, @"specialist/sundry\s+charges\s+will\s+be\s*" + Money, ReferenceRole: R.Agreed),
        new(F.VatAmount, K.Money, @"VAT\s+liability\s+on\s+this\s+repair\s+will\s+amount\s+to\s+some\s*" + Money, ReferenceRole: R.Agreed),
        new(F.Gross, K.Money, @"total\s+repair\s+cost\s+of\s*" + Money + @"\s*including\s+VAT", ReferenceRole: R.Agreed),

        new(F.ValuationGuide, K.Text, @"corresponding\s+(?<v>[A-Za-z]+(?:'s)?)\s+Guide"),
        new(F.Retail, K.Money, @"adjusted\s+retail\s+value[^£]{0,240}" + Money),
        new(F.Trade, K.Money, @"trade\s+value\s+is\s*" + Money),
        new(F.Mid, K.Money, @"mid\s+value\s+is\s*" + Money),
        new(F.PreAccidentValue, K.Money, @"Vehicle\s+Value:\s*" + Money),
        new(F.PreAccidentValue, K.Money, @"pre-accident\s+value\s+of\s+this\s+particular [A-Za-z]+ at\s*" + Money),
        new(F.Reserve, K.Money, @"repair\s+reserve\s+of\s*" + Money),
        new(F.MinimumRepairDays, K.Number, @"take\s+some\s*(?<v>\d+)\s*to\s*\d+\s*working\s+days", Unit: "days"),
        new(F.MaximumRepairDays, K.Number, @"take\s+some\s*\d+\s*to\s*(?<v>\d+)\s*working\s+days", Unit: "days")
    ];

    /// <summary>
    /// Laird prints two layouts under one issuer: the full assessment report and
    /// the supplementary report. The Supplementary heading controls — every
    /// supplement-role rule is gated on it, which also keeps the fee invoice
    /// printed at the back of a full report out of the repair totals.
    /// </summary>
    private static readonly ThirdPartyFieldRule[] LairdRules =
    [
        // One printed reference cell, whether the text engine kept it on a
        // single line ("26-1918326/2561054") or broke it around the
        // neighbouring header columns. Both halves keep their printed shape.
        new(F.ReportReference, K.Reference,
            @"(?<v>\d{2}-)(?:[^\n]*\n[ \t]*)?(?<v2>\d{6,}/\d{6,})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Reference, @"(?<v>[A-Z]{2,4}(?:/[A-Z]{2,4})?/\d{4,}/\d)", ReferenceRole: "your-ref"),
        new(F.ReportDate, K.Date,
            @"Our Reference\s+Your Reference\s+Date[^\n]*\n[^£]{0,200}?(?<v>\d{1,2}(?:st|nd|rd|th)\s+[A-Za-z]{3,9}\s+\d{4})"),
        new(F.Revision, K.Text, @"^\s*(?<v>Supplementary Report)\s*$"),
        new(F.Claimant, K.Text, @"Claimant[ \t]+(?<v>[^\n]{2,60}?)#END#", PartyRole: "claimant"),
        new(F.Claimant, K.Text, @"^\s*Re:\s*(?<v>[^\n]{2,60}?)\s*$", PartyRole: "claimant"),
        new(F.Make, K.Text, @"^[ \t]*Make[ \t]+(?<v>[A-Za-z][A-Za-z\- ]{1,24}?)#END#"),
        new(F.Model, K.Text, @"^[ \t]*Model[ \t]+(?<v>[^\n]{2,40}?)#END#"),
        new(F.Registration, K.Registration, @"^[ \t]*Registration[ \t]+(?<v>[A-Z0-9]{5,8})[ \t]*$"),
        new(F.Registration, K.Registration, @"registration\s+(?<v>[A-Z]{2}\d{2} ?[A-Z]{3})\b"),
        new(F.AccidentDate, K.Date, @"Accident Date[ \t]+(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.AccidentDate, K.Date, @"Road Traffic Accident on\s*(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Repairability, K.Text, @"^[ \t]*Status[ \t]+(?<v>[A-Za-z][A-Za-z ]{2,29}?)#END#"),
        new(F.Roadworthiness, K.Text, @"Legal Status[ \t]+(?<v>[A-Za-z][A-Za-z ]{2,19}?)#END#"),
        new(F.Severity, K.Text, @"Impact Magnitude[ \t]+(?<v>[A-Za-z][A-Za-z ]{2,29}?)#END#"),
        new(F.PreAccidentValue, K.Money, @"Engineer's Value[ \t]+" + Money),
        new(F.Retail, K.Money, @"Retail Value[ \t]+" + Money),
        new(F.Trade, K.Money, @"Trade Value[ \t]+" + Money),
        // Laird prints these two labels UNDER their value, so each rule is
        // anchored on the label in whichever order the cell was laid out. The
        // label still proves the value; only the side it sits on changes.
        new(F.ValuationGuide, K.Text,
            @"(?:Valuation|Source)[ \t]*\n[ \t]*(?<v>Glass'?e?s'?)\b"),
        new(F.Mileage, K.Mileage,
            @"(?<v>[\d,]+)[ \t]+(?:Miles|Km)\b[^\n]*\n[ \t]*Odometer\b", Unit: "miles"),
        new(F.MileageUnit, K.Text,
            @"[\d,]+[ \t]+(?<v>Miles|Km)\b[^\n]*\n[ \t]*Odometer\b"),
        new(F.Deduction, K.Money, @"we have deducted[ \t]*" + Money, Multiple: true),

        new(F.LabourHours, K.Number, @"^[ \t]*Hours[ \t]+(?<v>[\d.,]+)[ \t]*$",
            ReferenceRole: R.Assessed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"Hourly Rate[ \t]+" + Money, ReferenceRole: R.Assessed, Unit: "per hour"),
        new(F.LabourAmount, K.Money, @"Total Labour[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.Parts, K.Money, @"^[ \t]*Parts[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.PaintMaterials, K.Money, @"Paints\s*/\s*Materials[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.SpecialistCharges, K.Money, @"Specialist\s*/\s*Other Items[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.Net, K.Money, @"Sub Total[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.VatAmount, K.Money, @"^[ \t]*VAT[ \t]+" + Money, ReferenceRole: R.Assessed),
        new(F.Gross, K.Money, @"Total Estimated Cost[ \t]+" + Money, ReferenceRole: R.Assessed),

        new(F.LabourAmount, K.Money, @"Labour:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.LabourHours, K.Number, @"\(\s*(?<v>[\d.,]+)\s*hours at\s*£",
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading, Unit: "hours"),
        new(F.LabourRate, K.Money, @"hours at\s*" + Money + @"\s*per hour",
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading, Unit: "per hour"),
        new(F.Parts, K.Money, @"Parts:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.PaintMaterials, K.Money, @"Paint/materials:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.SpecialistCharges, K.Money, @"Specialist:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.Net, K.Money, @"Subtotal:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.VatAmount, K.Money, @"VAT:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),

        // Anchored at the start of the printed line, so the subtotal line is
        // not read a second time as the total: "Subtotal:" ends in "total:"
        // and an unanchored rule invents a conflict the document does not have.
        new(F.Gross, K.Money, @"^[ \t]*Total:[ \t]*" + Money,
            ReferenceRole: R.Supplement, CoLabel: SupplementaryHeading),
        new(F.SupplementReason, K.Text, @"^\s*(?<v>The repairing garage[^\n]{10,200})",
            CoLabel: SupplementaryHeading),
        new(F.Signatory, K.Text, @"Yours faithfully\s*\n\s*(?<v>[A-Za-z][^\n]{2,40}?)[ \t]*$")
    ];

    private static readonly ThirdPartyFieldRule[] MontgomeryRules =
    [
        new(F.ReportReference, K.Reference, @"Our Reference No:[ \t]*(?<v>[A-Za-z]{1,3}/\d{1,6})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Reference, @"Your Reference No:[ \t]*(?<v>[A-Za-z0-9/]+)", ReferenceRole: "your-ref"),
        new(F.Outcome, K.Text, @"^\s*(?<v>TOTAL LOSS|REPAIR)\s*$"),
        new(F.Claimant, K.Text, @"Client Name[ \t]+(?<v>[^\n]{2,60}?)#END#", PartyRole: "claimant"),
        new(F.VehicleLocation, K.Text, @"Inspection address[ \t]+(?<v>[^\n]{2,120}?)[ \t]*$"),
        new(F.AccidentDate, K.Date, @"Date of loss[ \t]+(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.InspectionDate, K.Date, @"Date of Inspection[ \t]+(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Make, K.Text, @"^[ \t]*Make[ \t]+(?<v>[A-Za-z][A-Za-z \-]{1,24}?)#END#"),
        new(F.Model, K.Text, @"^[ \t]*Model[ \t]+(?<v>[^\n]{1,40}?)#END#"),
        new(F.Registration, K.Registration, @"Registration[ \t]+(?<v>[A-Z0-9]{5,8})\b"),
        new(F.Vin, K.Text, @"V\.I\.N[ \t]+(?<v>[A-HJ-NPR-Z0-9]{11,17})"),
        new(F.Mileage, K.Mileage, @"Odometer[ \t]+(?<v>[\d,]+)", Unit: "miles"),
        new(F.Roadworthiness, K.Text, @"Roadworthy[ \t]+(?<v>YES|NO)\b"),
        new(F.OutcomeReason, K.Text, @"Reason[ \t]+(?<v>[^\n]{2,60}?)[ \t]*$"),
        new(F.Airbags, K.Text, @"Airbag[ \t]+(?<v>YES|NO)\b"),
        new(F.Restraints, K.Text, @"Pre-tensioners[ \t]+(?<v>YES|NO)\b"),
        new(F.Severity, K.Text, @"severity of the damage was\s*(?<v>[A-Za-z]{3,12})"),
        new(F.Tyres, K.Text, @"^[ \t]*(?<v>\d+mm[^\n]*\d+mm,?)[ \t]*$"),
        new(F.Narrative, K.Text, @"^\s*Summary\s*$", Section: true),
        new(F.Comments, K.Text, @"^\s*Comments\s*$", Section: true),
        new(F.EngineerName, K.Text, @"Consulting Motor Engineers[^\n]*\n[ \t]*(?<v>[A-Z][a-z]+ [A-Z][a-z]+)"),

        new(F.LabourHours, K.Number, @"^[ \t]*Hours[ \t]+(?<v>[\d.,]+)[ \t]*$",
            ReferenceRole: R.Assessed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"Hourly rate[ \t]+" + BareMoney,
            ReferenceRole: R.Assessed, Unit: "per hour"),
        new(F.LabourAmount, K.Money, @"Total Labour[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Parts, K.Money, @"^[ \t]*Parts[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.PaintMaterials, K.Money, @"Paint/Materials[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.SpecialistCharges, K.Money, @"^[ \t]*Specialist[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Net, K.Money, @"Sub Total[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.VatAmount, K.Money, @"^[ \t]*VAT[ \t]+" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Gross, K.Money, @"Total Reserve[ \t]+" + BareMoney, ReferenceRole: R.Assessed),

        new(F.ValuationGuide, K.Text, @"^[ \t]*(?<v>Glass'?e?s'?)[ \t]+[\d,]"),
        new(F.Trade, K.Money, @"^[ \t]*Glass'?e?s'?[ \t]+(?<v>[\d,]+)[ \t]+[\d,]+"),
        new(F.Retail, K.Money, @"^[ \t]*Glass'?e?s'?[ \t]+[\d,]+[ \t]+(?<v>[\d,]+)"),
        new(F.PreAccidentValue, K.Money, @"^[ \t]*Valuation[ \t]+(?<v>[\d,]+)[ \t]*$"),

        // The printed label ("Urban edition adjustment") is not one of the two
        // typed adjustment slots, so the whole printed cell is kept as the raw
        // value and the reconciliation reads the row. Nothing is renamed.
        new(F.ValuationAdjustment, K.Money,
            @"^[ \t]*(?:[A-Za-z][A-Za-z ]{0,30})?adjustment[ \t]+(?<v>[\d,]+)[ \t]*$",
            Multiple: true, RawWholeMatch: true),

        new(F.FinalValue, K.Money, @"VEHICLE VALUE[ \t]*" + Money),
        new(F.SalvageValue, K.Money, @"Salvage value[ \t]+(?<v>[\d,]+(?:\.\d{2})?)")
    ];

    private static readonly ThirdPartyFieldRule[] SPrintRules =
    [
        new(F.ReportReference, K.Reference, @"Our Ref[ \t]*:[ \t]*(?<v>\d{3,})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Text, @"Your Ref[ \t]*:[ \t]*(?<v>[A-Z]{2,4}[ \t]*\d{4,}[ \t]*\d)", ReferenceRole: "your-ref"),
        new(F.ReportDate, K.Date, @"Date of Report[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.InspectionDate, K.Date, @"Date of Inspection[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.AccidentDate, K.Date, @"Date of Accident[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.Claimant, K.Text, @"Insured[ \t]*:[ \t]*(?<v>[^\n]{2,60}?)[ \t]*$", PartyRole: "claimant"),
        new(F.Registration, K.Registration, @"Reg No[ \t]*:[ \t]*(?<v>[A-Z0-9]{5,8})\b"),
        new(F.Make, K.Text, @"Make[ \t]*:[ \t]*(?<v>[A-Z][A-Z\- ]{1,19}?)#END#"),
        new(F.Model, K.Text, @"Model[ \t]*:[ \t]*(?<v>[A-Z][A-Z0-9\- ]{1,24}?)#END#"),
        new(F.Variant, K.Text, @"Spec[ \t]*:[ \t]*(?<v>[A-Z0-9][^\n]{1,39}?)#END#"),
        new(F.Vin, K.Text, @"Chassis No[ \t]*:[ \t]*(?<v>[A-HJ-NPR-Z0-9]{11,17})"),
        new(F.Mileage, K.Mileage, @"Mileage[ \t]*:[ \t]*(?<v>[\d,]+)", Unit: "miles"),
        new(F.MileageUnit, K.Text, @"Mileage[ \t]*:[ \t]*[\d,]+[ \t]*(?<v>Miles|Km)\b"),
        new(F.VehicleLocation, K.Text, @"Inspection Location[ \t]*:[ \t]*(?<v>[^\n]{2,40}?)[ \t]*$"),
        new(F.Repairer, K.Text, @"Repairer[ \t]*:[ \t]*(?<v>[^\n]{3,120}?)[ \t]*$", PartyRole: "repairer"),
        new(F.Repairability, K.Text, @"Vehicle Status[ \t]*:[ \t]*(?<v>[A-Z]{4,20})"),
        new(F.Roadworthiness, K.Text, @"\b(?<v>UNROADWORTHY|ROADWORTHY)\b"),
        new(F.Severity, K.Text, @"Body[ \t]*:[ \t]*(?<v>HEAVY|MODERATE|LIGHT|MEDIUM|SEVERE)\b"),
        new(F.MinimumRepairDays, K.Number, @"Repair Time In Days[ \t]*:[ \t]*(?<v>\d+)", Unit: "days"),
        new(F.Comments, K.Text, @"^\s*Comments\s*/\s*Repair Notes[ \t]*:\s*$", Section: true),
        new(F.Declaration, K.Text,
            @"(?<v>I confirm that I have made clear which facts and matters referred to in this report are within my own knowledge and which are not\.)"),

        new(F.LabourRate, K.Money, @"Labour Rate[ \t]*" + Money, ReferenceRole: R.Assessed, Unit: "per hour"),
        new(F.LabourAmount, K.Money, @"^[ \t]*Labour[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.PaintMaterials, K.Money, @"Paint\s*/\s*Materials[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.Parts, K.Money, @"^[ \t]*Parts[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.SpecialistCharges, K.Money, @"^[ \t]*Specialist[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.Net, K.Money, @"Total Exc VAT[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.Gross, K.Money, @"Total Inc VAT[ \t]*" + Money, ReferenceRole: R.Assessed),
        new(F.VatRate, K.Number, @"V\.A\.T[ \t]*@[ \t]*(?<v>\d{1,2})[ \t]*%",
            ReferenceRole: R.Assessed, Unit: "percent"),
        new(F.VatAmount, K.Money, @"V\.A\.T[ \t]*@[ \t]*\d{1,2}[ \t]*%[ \t]*" + Money, ReferenceRole: R.Assessed),

        // A contract repair figure is its own printed role, never the ordinary
        // total: the ordinary totals may legitimately be zero beside it.
        new(F.Net, K.Money, @"Contract Repair[ \t]*" + Money, ReferenceRole: R.ContractRepair),

        // The notes name the original amounts explicitly ("NOTING ORIGINAL
        // LABOUR £3303, PARTS, £2652 ..."). They are a third printed role, so
        // they are read from their own labels and never merged with either the
        // zero ordinary totals or the contract figure.
        // Each is anchored on the note's own opening words, so none of them can
        // reach the ordinary totals table printed above it — those legitimately
        // read zero, and letting a rule match both would invent a conflict
        // between two amounts the document keeps apart on purpose.
        new(F.LabourAmount, K.Money,
            @"NOTING\s+ORIGINAL\s+LABOUR[ \t]*" + Money, ReferenceRole: R.Initial),
        new(F.Parts, K.Money,
            @"NOTING\s+ORIGINAL[\s\S]{0,120}?PARTS,?[ \t]*" + Money, ReferenceRole: R.Initial),
        new(F.PaintMaterials, K.Money,
            @"NOTING\s+ORIGINAL[\s\S]{0,120}?MATERIALS[ \t]*" + Money, ReferenceRole: R.Initial),
        new(F.SpecialistCharges, K.Money,
            @"NOTING\s+ORIGINAL[\s\S]{0,120}?SPEC[ \t]*" + Money, ReferenceRole: R.Initial),

        new(F.Excess, K.Money, @"^[ \t]*Excess[ \t]*" + Money),
        new(F.PreAccidentValue, K.Money, @"Vehicle Market Value[ \t]*" + Money),
        new(F.Retail, K.Money, @"Glass\S{0,2}s Retail Value[ \t]*" + Money),
        new(F.Trade, K.Money, @"Glass\S{0,2}s Trade Value[ \t]*" + Money),
        new(F.FinalValue, K.Money, @"Engineers Valuation Figure[ \t]*" + Money),
        new(F.SalvageValue, K.Money, @"Salvage Value[ \t]*" + Money),
        new(F.SalvageCategory, K.Text, @"Motor Salvage Category[ \t]*:[ \t]*(?<v>[A-Z])\b")
    ];

    /// <summary>
    /// The printed labels that end a cell in the shared narrative block. They
    /// come from the corpus's own vehicle table, so a value stops where the
    /// next label starts however the PDF engine spaced the columns.
    /// </summary>
    private const string NarrativeLabels =
        @"Colour|Speedo|Reg No|Registered|Type|Trans|Vin No|MOT Exp|Mods|Cond|Audio|Manf'd"
        + @"|Brakes|Tax Exp|Steering|Fuel|Extras|C\.C\.|Air Bags|BHP|Deployed|Damage|Incident"
        + @"|Vehicle Value|Repair Cost|Roadworthy|Spare Tyre|Centre Belt|[LR]/H/[FR]"
        + @"|Date|Our Ref|Your Ref|Claim No|Client|Continued|Page";

    private const string LairdLabels =
        @"Claimant|Accident Date|Instruction Date|Impact Diagram|Registration Date|Gearbox"
        + @"|Body Type|VIN|Condition|Engine|Fuel|Trade Value|Retail Value|Make|Model|Status"
        + @"|Impact Magnitude|Odometer|Colour|Driven Axle|Valuation|Source|Euro NCAP"
        + @"|Vehicle Width|Vehicle Length|Rating|Value";

    private const string MontgomeryLabels =
        @"Registration|Type|V\.I\.N|Tax Expiry|Date of Reg|Gearbox|Brakes|Condition|Interior"
        + @"|Exterior|Reason|Pre-tensioners|deployed|Odometer|Colour|Engine|Fuel|Steering"
        + @"|Extras|Modifications|Roadworthy|Airbag|Make|Model|Trade|Retail";

    private const string SPrintLabels =
        @"OSF|NSF|OSR|NSR|Others|Steering|Footbrake|Handbrake|Seatbelts|Mechanical|Body"
        + @"|Inspection Location|Vehicle Status|Pre-Accident Condition|Cause of Damage"
        + @"|Date of Report|Date of Inspection|Date Instructed|Date of Accident|Miles|Km";

    /// <summary>
    /// The rule table per family. John R Bell is deliberately empty: the only
    /// original in the corpus is scan-only, so no printed layout has been
    /// observed and no rule is guessed for it — its fields stay unavailable
    /// until OCR text reaches this engine (INTK-032).
    /// </summary>
    private static readonly Dictionary<ThirdPartyReportFamily, ThirdPartyFamilyRules> Rules = new()
    {
        [ThirdPartyReportFamily.Connexus] = new(NarrativeLabels, NarrativeRules),
        [ThirdPartyReportFamily.ExclusiveErehr] = new(NarrativeLabels, NarrativeRules),
        [ThirdPartyReportFamily.EvaBodyshop] = new(NarrativeLabels, NarrativeRules),
        [ThirdPartyReportFamily.Laird] = new(LairdLabels, LairdRules),
        [ThirdPartyReportFamily.Montgomery] = new(MontgomeryLabels, MontgomeryRules),
        [ThirdPartyReportFamily.SPrint] = new(SPrintLabels, SPrintRules),
        [ThirdPartyReportFamily.JohnRBell] = new(string.Empty, [])
    };

    private static readonly Dictionary<ThirdPartyReportFamily, IReadOnlyList<CompiledRule>> CompiledRules =
        Rules.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<CompiledRule>)
                [.. entry.Value.Rules.Select(rule => CompiledRule.Compile(rule, entry.Value.LabelBoundary))]);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Reads one source into a third-party report candidate. A source with no
    /// readable text, no known signature, or an explicit non-report role yields
    /// no candidate at all — the selection records why.
    /// </summary>
    public static ThirdPartyReportExtractionResult Extract(
        IntakeSourceReadResult readResult,
        ThirdPartyReportSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(context);

        var pages = ThirdPartySourcePage.Read(readResult);
        var selection = ThirdPartyReportProfiles.Select(pages, context);
        var documentRole = ThirdPartyReportProfiles.DocumentRoleCode(selection.DocumentRole);
        var rows = new List<SourceFieldCandidate> { selection.Issuer };

        // Every scan-only page is recorded with its own locator before any
        // field is read. That is the John R Bell case: a page whose text the
        // reader could not take is a page whose critical values a person must
        // check against the original, and the row says exactly which page.
        rows.AddRange(ScannedPages(readResult, context, documentRole));

        if (selection.Outcome != ThirdPartySelectionOutcome.Selected || selection.Family is not { } family)
        {
            return Complete(selection, null, rows, readResult.RequiresOcr, context, documentRole);
        }

        var observed = Observe(family, pages, context, documentRole);
        rows.AddRange(observed.Order.SelectMany(key => observed.Rows[key]));
        rows.AddRange(Media(readResult, context, documentRole));

        var candidate = Project(selection, observed, rows, context);
        return Complete(selection, candidate, rows, readResult.RequiresOcr, context, documentRole);
    }

    /// <summary>
    /// Reconciles the rows that were read and records each finding as its own
    /// source row beside them (INTK-056).
    ///
    /// A finding is computed from the printed values only, and it reaches
    /// storage as an ordinary <see cref="SourceFieldCandidate"/> under the
    /// <see cref="ThirdPartyReportFields.FindingPrefix"/> namespace: raw text
    /// is the finding's own statement including the printed values it compared,
    /// normalized value is the stable finding code, and the locator is the one
    /// the compared rows carry. No printed value is edited, replaced or
    /// recomputed on the way — the row that says "26.2 hours at 90 is 2358, not
    /// the printed labour 1582.2" sits beside three rows that still read 26.20,
    /// 90.00 and 1582.20.
    ///
    /// Findings are appended after the reconciliation has run, so a finding is
    /// never itself an input to one.
    /// </summary>
    private static ThirdPartyReportExtractionResult Complete(
        ThirdPartyReportSelection selection,
        ThirdPartyReportCandidate? candidate,
        List<SourceFieldCandidate> rows,
        bool requiresOcr,
        ThirdPartyReportSourceContext context,
        string documentRole)
    {
        var findings = ThirdPartyReportValidation.Check(selection, candidate, rows, requiresOcr);
        rows.AddRange(FindingRows(findings, context, documentRole, selection.Issuer));
        return new(selection, candidate, rows, findings);
    }

    /// <summary>
    /// One row per finding, in the order the reconciliation raised them. Each
    /// carries the document, version, occurrence and hash of the rows it
    /// compares (from the shared context), the reader version, and the
    /// <see cref="ThirdPartyReportValidation.PolicyVersion"/> that stamps the
    /// finding rules rather than the extraction rules — a later change to the
    /// arithmetic is then distinguishable from a later change to the reading.
    /// </summary>
    private static IEnumerable<SourceFieldCandidate> FindingRows(
        IReadOnlyList<ThirdPartyReportFinding> findings,
        ThirdPartyReportSourceContext context,
        string documentRole,
        SourceFieldCandidate issuer)
    {
        foreach (var finding in findings)
        {
            // The locator of the rows the finding compared, so an operator
            // opens the page the contradiction is printed on. Where a finding
            // has no evidence row of its own, the issuer row's locator is the
            // honest fallback: it is the page the document was identified from.
            var locator = finding.Evidence.Count > 0 ? finding.Evidence[0] : issuer;
            yield return ThirdPartySourceCandidates.Create(
                context,
                F.Finding(finding.Code),
                documentRole,
                rawValue: finding.Message,
                normalizedValue: finding.Code,
                page: locator.Page,
                sourceLabel: locator.SourceLabel,
                policyVersion: ThirdPartyReportValidation.PolicyVersion,
                disposition: FindingDisposition(finding.Kind),
                referenceRole: FindingRole(finding),
                region: "finding");
        }
    }

    /// <summary>
    /// What the operator must do with a finding, expressed in the only
    /// vocabulary a source row has. A printed contradiction is
    /// <see cref="SourceCandidateDisposition.Conflicting"/>; every other
    /// finding is <see cref="SourceCandidateDisposition.Ambiguous"/> — an
    /// observation to weigh. A finding is never Usable, because a finding is
    /// not a value: nothing may accept one as a figure.
    /// </summary>
    private static SourceCandidateDisposition FindingDisposition(ThirdPartyFindingKind kind) =>
        kind == ThirdPartyFindingKind.Conflict
            ? SourceCandidateDisposition.Conflicting
            : SourceCandidateDisposition.Ambiguous;

    /// <summary>
    /// The printed amount role a finding is about, taken from the rows it
    /// compared rather than restated: a finding that spans two roles (an
    /// initial figure against an agreed one) carries none, because naming
    /// either would misfile it.
    /// </summary>
    private static string FindingRole(ThirdPartyReportFinding finding)
    {
        var roles = finding.Evidence
            .Select(row => row.ReferenceRole)
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return roles.Count == 1 ? roles[0] : string.Empty;
    }

    /// <summary>
    /// One row per page the reader marked scan-only: the page's fields are
    /// unavailable (Missing, never invented) and the locator is kept so the
    /// operator can open that exact page. No OCR engine is added here — the
    /// approved OCR boundary is the existing reader's, and until it supplies
    /// text these pages stay explicitly unread.
    /// </summary>
    private static IReadOnlyList<SourceFieldCandidate> ScannedPages(
        IntakeSourceReadResult readResult,
        ThirdPartyReportSourceContext context,
        string documentRole) =>
        [.. readResult.ScannedPdfPages
            .OrderBy(page => page.PageNumber)
            .Select(page => ThirdPartySourceCandidates.Create(
                context,
                F.PageRequiresHumanVerification,
                documentRole,
                rawValue: null,
                normalizedValue: null,
                page: page.PageNumber,
                sourceLabel: $"{page.SourceLabel}, page {page.PageNumber}",
                policyVersion: ProfileVersion,
                disposition: SourceCandidateDisposition.Missing))];

    private static IReadOnlyList<SourceFieldCandidate> Media(
        IntakeSourceReadResult readResult,
        ThirdPartyReportSourceContext context,
        string documentRole) =>
        [.. readResult.AssetCandidates
            .Where(asset => asset.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .Select(asset => ThirdPartySourceCandidates.Create(
                context,
                F.Photograph,
                documentRole,
                asset.FileName,
                null,
                asset.PageNumber,
                asset.SourceLabel,
                ProfileVersion,
                SourceCandidateDisposition.Usable))];

    /// <summary>
    /// Applies one family's rules and resolves each field's disposition: one
    /// distinct value is Usable, several are all persisted as Conflicting, and a
    /// declared field with none is persisted as Missing.
    /// </summary>
    private static ObservedFields Observe(
        ThirdPartyReportFamily family,
        IReadOnlyList<ThirdPartySourcePage> pages,
        ThirdPartyReportSourceContext context,
        string documentRole)
    {
        var observed = new Dictionary<FieldKey, List<Observation>>();
        var declared = new List<FieldKey>();
        foreach (var rule in Compiled(family))
        {
            var key = new FieldKey(rule.Rule.Field, rule.Rule.ReferenceRole, rule.Rule.PartyRole);
            if (!declared.Contains(key))
            {
                declared.Add(key);
            }

            foreach (var page in pages)
            {
                if (rule.CoLabel is not null && !rule.CoLabel.IsMatch(page.Text))
                {
                    continue;
                }

                // A section rule reads its heading's body; a label rule reads
                // the captured value, and keeps the whole printed cell as the
                // raw text where the projection has no typed slot for its
                // label. Normalization always runs on the captured value.
                var raws = rule.Rule.Section
                    ? ThirdPartySections.Read(page.Text, rule.Value).Select(body => (body, body))
                    : rule.Value.Matches(page.Text).Select(match => (
                        Value: Captured(match),
                        Raw: rule.Rule.RawWholeMatch ? match.Value : Captured(match)));
                foreach (var (value, raw) in raws)
                {
                    var normalized = Normalize(rule.Rule.Kind, value);
                    if (normalized is null)
                    {
                        continue;
                    }

                    if (!observed.TryGetValue(key, out var values))
                    {
                        values = [];
                        observed[key] = values;
                    }

                    if (values.Any(item =>
                            string.Equals(item.Normalized, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    values.Add(new(rule.Rule, page, Collapse(raw), normalized));
                }
            }
        }

        var rows = new Dictionary<FieldKey, List<SourceFieldCandidate>>();
        foreach (var key in declared)
        {
            if (!observed.TryGetValue(key, out var values) || values.Count == 0)
            {
                rows[key] =
                [
                    ThirdPartySourceCandidates.Create(
                        context, key.Field, documentRole, null, null, null,
                        pages.Count > 0 ? pages[0].SourceLabel : string.Empty,
                        ProfileVersion, SourceCandidateDisposition.Missing,
                        key.PartyRole, key.ReferenceRole)
                ];
                continue;
            }

            var disposition = values[0].Rule.Force
                ?? (values.Count > 1 && !values[0].Rule.Multiple
                    ? SourceCandidateDisposition.Conflicting
                    : SourceCandidateDisposition.Usable);
            rows[key] =
            [
                .. values.Select(value => ThirdPartySourceCandidates.Create(
                    context, key.Field, documentRole, value.Raw, value.Normalized, value.Page.Page,
                    value.Page.SourceLabel, ProfileVersion, disposition,
                    key.PartyRole, key.ReferenceRole,
                    value.Rule.Unit,
                    value.Rule.Kind == K.Money ? "GBP" : null,
                    region: value.Rule.Section ? "section" : "label"))
            ];
        }

        return new(declared, rows);
    }

    private static ThirdPartyReportCandidate Project(
        ThirdPartyReportSelection selection,
        ObservedFields observed,
        IReadOnlyList<SourceFieldCandidate> rows,
        ThirdPartyReportSourceContext context)
    {
        var lookup = new Lookup(observed, selection.Issuer);
        var identity = new ThirdPartyReportIdentity(
            lookup.Text(F.Issuer),
            lookup.Text(F.EngineerName),
            lookup.Text(F.EngineerQualifications),
            lookup.Text(F.ReportReference, "our-ref"),
            lookup.Text(F.ClaimReference, "your-ref"),
            lookup.Date(F.ReportDate),
            lookup.Text(F.Revision),
            lookup.Text(F.Amendment),
            // A base report is linked only where the store has resolved a
            // printed base reference to a document; C never invents the link.
            null);

        var vehicle = new ThirdPartyReportVehicle(
            lookup.Text(F.Registration),
            lookup.Text(F.Make),
            lookup.Text(F.Model),
            lookup.Text(F.Variant),
            lookup.Text(F.Vin),
            lookup.Number(F.Mileage),
            lookup.Text(F.MileageUnit));

        var parties = new ThirdPartyReportParties(
            lookup.Text(F.Claimant, partyRole: "claimant"),
            lookup.Text(F.Repairer, partyRole: "repairer"),
            lookup.Text(F.RepairerAddress, partyRole: "repairer"),
            lookup.Text(F.VehicleLocation),
            lookup.Date(F.AccidentDate),
            lookup.Date(F.InspectionDate));

        var damage = new ThirdPartyReportDamage(
            lookup.Text(F.Outcome),
            lookup.Text(F.Repairability),
            lookup.Text(F.Roadworthiness),
            lookup.Text(F.OutcomeReason),
            lookup.Text(F.Severity),
            lookup.Text(F.Narrative),
            lookup.Text(F.PriorDamage),
            lookup.Text(F.Tyres),
            lookup.Text(F.Restraints),
            lookup.Text(F.Airbags),
            lookup.TextList(F.DamageZone));

        var valuation = new ThirdPartyReportValuation(
            lookup.Text(F.ValuationGuide),
            lookup.Date(F.ValuationGuideDate),
            lookup.Number(F.Trade),
            lookup.Number(F.Retail),
            lookup.Number(F.Mid),
            lookup.Number(F.PreAccidentValue),
            lookup.Number(F.MileageAdjustment),
            lookup.Number(F.ConditionAdjustment),
            lookup.Number(F.FinalValue),
            lookup.Text(F.SalvageCategory),
            lookup.Number(F.SalvageValue),
            lookup.Number(F.SalvageBid),
            lookup.Number(F.Excess),
            lookup.Number(F.Reserve),
            lookup.Number(F.CashInLieu),
            lookup.NumberList(F.Deduction));

        var declaration = new ThirdPartyReportDeclaration(
            lookup.Number(F.MinimumRepairDays),
            lookup.Number(F.MaximumRepairDays),
            lookup.Text(F.RequestedInspectionMethod),
            lookup.Text(F.ObservedInspectionMethod),
            lookup.Text(F.Comments),
            lookup.Text(F.SupplementReason),
            lookup.Text(F.Declaration),
            lookup.Text(F.Signatory),
            [.. rows
                .Where(row => row.Field == F.Photograph)
                .Select(row => new ThirdPartyReportFact<Guid?>(null, row))],
            // No source in the corpus distinguishes a diagram from a
            // photograph, so no diagram role is asserted.
            []);

        return new(
            context.DocumentId,
            context.DocumentVersionId,
            context.IntakeAssetId,
            context.Sha256,
            context.Occurrence,
            identity,
            vehicle,
            parties,
            damage,
            [.. Estimates(lookup)],
            valuation,
            declaration);
    }

    private static IEnumerable<ThirdPartyReportEstimate> Estimates(Lookup lookup)
    {
        foreach (var role in Enum.GetValues<ThirdPartyEstimateRole>())
        {
            var code = R.Code(role);
            var estimate = new ThirdPartyReportEstimate(
                role,
                lookup.Number(F.LabourHours, code),
                lookup.Number(F.LabourRate, code),
                lookup.Number(F.LabourAmount, code),
                lookup.Number(F.PaintMaterials, code),
                lookup.Number(F.Parts, code),
                lookup.Number(F.SpecialistCharges, code),
                lookup.Number(F.AdditionalCharges, code),
                lookup.Number(F.Discounts, code),
                lookup.Number(F.Net, code),
                lookup.Number(F.VatRate, code),
                lookup.Number(F.VatAmount, code),
                lookup.Number(F.Gross, code));
            if (HasValue(estimate))
            {
                yield return estimate;
            }
        }
    }

    private static bool HasValue(ThirdPartyReportEstimate estimate) =>
        estimate.LabourHours?.Value is not null
        || estimate.LabourRate?.Value is not null
        || estimate.LabourAmount?.Value is not null
        || estimate.PaintMaterials?.Value is not null
        || estimate.Parts?.Value is not null
        || estimate.SpecialistCharges?.Value is not null
        || estimate.AdditionalCharges?.Value is not null
        || estimate.Discounts?.Value is not null
        || estimate.Net?.Value is not null
        || estimate.VatRate?.Value is not null
        || estimate.VatAmount?.Value is not null
        || estimate.Gross?.Value is not null;

    private static string? Normalize(ThirdPartyValueKind kind, string raw)
    {
        var value = Collapse(raw);
        if (value.Length == 0)
        {
            return null;
        }

        return kind switch
        {
            K.Text => value,
            K.Reference => value.Replace(" ", string.Empty, StringComparison.Ordinal),
            K.Money or K.Number => Number(value),
            K.Date => InstructionFieldEngine.CanonicalDate(value),
            K.Registration => Registration(value),
            K.Mileage => InstructionFieldEngine.ParseMileage(value)?.ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static string? Number(string value)
    {
        var trimmed = value
            .Replace(PoundSign, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    private static string? Registration(string value) =>
        InstructionFieldEngine.IsUkRegistration(value)
            ? InstructionFieldEngine.NormalizeRegistration(value)
            : null;

    private static string Collapse(string value) => WhitespaceRegex.Replace(value, " ").Trim();

    /// <summary>
    /// The printed value: group <c>v</c>, plus group <c>v2</c> where a rule
    /// reads one cell the text engine broke across two lines. Nothing else is
    /// ever joined — two separate printed cells stay two observations.
    /// </summary>
    private static string Captured(Match match) =>
        match.Groups["v2"].Success
            ? match.Groups["v"].Value + match.Groups["v2"].Value
            : match.Groups["v"].Value;

    private static IReadOnlyList<CompiledRule> Compiled(ThirdPartyReportFamily family) =>
        CompiledRules.TryGetValue(family, out var compiled) ? compiled : [];

    private sealed record CompiledRule(ThirdPartyFieldRule Rule, Regex Value, Regex? CoLabel)
    {
        /// <summary>
        /// The token every free-text rule ends with. A printed cell ends at a
        /// run of two or more spaces, at the end of its line, or where the next
        /// printed label begins — the three shapes the reference pack's text
        /// engine and the production reader between them produce.
        /// </summary>
        private const string EndToken = "#END#";

        public static CompiledRule Compile(ThirdPartyFieldRule rule, string labelBoundary)
        {
            var boundary = rule.Until ?? labelBoundary;
            var pattern = rule.Pattern.Contains(EndToken, StringComparison.Ordinal)
                ? rule.Pattern.Replace(
                    EndToken,
                    boundary.Length == 0
                        ? @"(?=[ \t]{2,}|[ \t]*$)"
                        : $@"(?=[ \t]{{2,}}|[ \t]*$|[ \t]+(?:{boundary})\b)",
                    StringComparison.Ordinal)
                : rule.Pattern;
            return new(
                rule,
                ThirdPartyRegex.CreateMultiline(pattern),
                rule.CoLabel is null ? null : ThirdPartyRegex.CreateMultiline(rule.CoLabel));
        }
    }

    private sealed record Observation(
        ThirdPartyFieldRule Rule,
        ThirdPartySourcePage Page,
        string Raw,
        string Normalized);

    private readonly record struct FieldKey(string Field, string ReferenceRole, string PartyRole);

    /// <summary>
    /// The observed rows plus the order their rules declared them in. The order
    /// is what makes a replay byte-identical: a dictionary's enumeration order
    /// is not part of any contract, and two fields that both list values would
    /// otherwise be free to swap places between runs.
    /// </summary>
    private sealed record ObservedFields(
        IReadOnlyList<FieldKey> Order,
        Dictionary<FieldKey, List<SourceFieldCandidate>> Rows);

    /// <summary>
    /// Reads the typed projection off the persisted rows. A conflicting field
    /// exposes no single value: both rows stay in the candidate list, and the
    /// typed fact carries the conflict rather than a chosen winner.
    /// </summary>
    private sealed class Lookup(
        ObservedFields observed,
        SourceFieldCandidate issuer)
    {
        public ThirdPartyReportFact<string?>? Text(
            string field,
            string referenceRole = "",
            string partyRole = "")
        {
            if (field == F.Issuer)
            {
                return new(Usable(issuer) ? issuer.NormalizedValue : null, issuer);
            }

            var row = First(field, referenceRole, partyRole);
            return row is null ? null : new(Usable(row) ? row.NormalizedValue : null, row);
        }

        public ThirdPartyReportFact<decimal?>? Number(string field, string referenceRole = "")
        {
            var row = First(field, referenceRole, string.Empty);
            if (row is null)
            {
                return null;
            }

            var value = Usable(row)
                        && decimal.TryParse(
                            row.NormalizedValue,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var parsed)
                ? parsed
                : (decimal?)null;
            return new(value, row);
        }

        public ThirdPartyReportFact<DateOnly?>? Date(string field, string referenceRole = "")
        {
            var row = First(field, referenceRole, string.Empty);
            return row is null
                ? null
                : new(Usable(row) ? InstructionFieldEngine.ParseDate(row.NormalizedValue) : null, row);
        }

        public IReadOnlyList<ThirdPartyReportFact<string?>> TextList(string field) =>
            [.. All(field).Select(row => new ThirdPartyReportFact<string?>(row.NormalizedValue, row))];

        public IReadOnlyList<ThirdPartyReportFact<decimal?>> NumberList(string field) =>
            [.. All(field).Select(row => new ThirdPartyReportFact<decimal?>(
                decimal.TryParse(
                    row.NormalizedValue,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null,
                row))];

        private static bool Usable(SourceFieldCandidate row) =>
            row.Disposition == SourceCandidateDisposition.Usable;

        private SourceFieldCandidate? First(string field, string referenceRole, string partyRole) =>
            observed.Rows.TryGetValue(new(field, referenceRole, partyRole), out var found)
            && found.Count > 0
                ? found[0]
                : null;

        /// <summary>
        /// Every observed row of one field, walked in the rule table's declared
        /// order rather than a dictionary's, so replaying the same bytes yields
        /// the same list in the same order.
        /// </summary>
        private IEnumerable<SourceFieldCandidate> All(string field) =>
            observed.Order
                .Where(key => key.Field == field)
                .SelectMany(key => observed.Rows[key])
                .Where(row => row.Disposition != SourceCandidateDisposition.Missing);
    }
}

/// <summary>
/// The bridge from a read third-party report onto the retained-analysis record
/// C01 already owns (INTK-031). It adds no persistence of its own: a report's
/// candidates are ordinary source candidates on the existing analysis row, so
/// the Received page renders them through the provenance chips it already uses
/// and <see cref="ISourceCandidateQueries"/> reads them back unchanged.
///
/// It converts nothing into a decision. Every row is a candidate, the printed
/// amount roles stay separate, and an accepted Engineer value remains Stream
/// B's own command.
/// </summary>
public static class ThirdPartyReportAnalysis
{
    /// <summary>Recorded on every row, so a report candidate is identifiable as one.</summary>
    public const string PolicyKey = "third-party-report";

    /// <summary>
    /// Whether this source is one the report reader has anything to say about:
    /// at least one document signature matched. A document with readable text
    /// and no signature is left entirely alone — writing an empty analysis for
    /// every unrelated attachment would bury the ones that matter.
    /// </summary>
    public static bool IsRecordable(ThirdPartyReportSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Matches.Count > 0;
    }

    /// <summary>
    /// The analysis outcome for a selection: a matched family is
    /// <see cref="RetainedInstructionAnalysisOutcome.Analyzed"/>, several
    /// matched signatures are
    /// <see cref="RetainedInstructionAnalysisOutcome.Ambiguous"/>, and an
    /// explicit non-report role is
    /// <see cref="RetainedInstructionAnalysisOutcome.NoProfile"/> — the
    /// document was read and is genuinely not a report, which is the answer
    /// the negative cases need rather than an invented verdict.
    /// </summary>
    public static RetainedInstructionAnalysisOutcome Outcome(ThirdPartyReportSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Outcome switch
        {
            ThirdPartySelectionOutcome.Selected => RetainedInstructionAnalysisOutcome.Analyzed,
            ThirdPartySelectionOutcome.Ambiguous => RetainedInstructionAnalysisOutcome.Ambiguous,
            _ => RetainedInstructionAnalysisOutcome.NoProfile
        };
    }

    /// <summary>
    /// Maps every read source row onto the recorded-candidate shape. Order is
    /// the order the rows were read, and <c>Occurrence</c> is carried from the
    /// source context rather than re-derived, so the same bytes always produce
    /// the same record.
    /// </summary>
    public static IReadOnlyList<RetainedInstructionCandidate> ToCandidates(
        ThirdPartyReportExtractionResult result,
        string readerKey,
        string readerVersion)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(readerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(readerVersion);
        return
        [
            .. result.Candidates.Select(row => new RetainedInstructionCandidate(
                row.Id,
                row.DocumentRole,
                row.Field,
                row.PartyRole.Length == 0 ? null : row.PartyRole,
                row.ReferenceRole.Length == 0 ? null : row.ReferenceRole,
                row.RawValue,
                row.NormalizedValue,
                row.Unit,
                row.Currency,
                row.SourceLabel,
                row.Page,
                row.Occurrence,
                readerKey,
                readerVersion,
                PolicyKey,
                row.PolicyVersion,
                row.Disposition))
        ];
    }
}
