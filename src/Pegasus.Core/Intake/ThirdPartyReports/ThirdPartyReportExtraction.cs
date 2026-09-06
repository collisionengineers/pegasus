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
    public const string BaseReportReference = "identity.base.report.reference";

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

    public const string Photograph = "media.photograph";
    public const string Diagram = "media.diagram";
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

    public static ThirdPartyEstimateRole Parse(string role) => role switch
    {
        Initial => ThirdPartyEstimateRole.Initial,
        Claimed => ThirdPartyEstimateRole.Claimed,
        Assessed => ThirdPartyEstimateRole.Assessed,
        Agreed => ThirdPartyEstimateRole.Agreed,
        Revised => ThirdPartyEstimateRole.Revised,
        Supplement => ThirdPartyEstimateRole.Supplement,
        ContractRepair => ThirdPartyEstimateRole.ContractRepair,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown printed amount role.")
    };

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
    SourceCandidateDisposition? Force = null);

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

    private const string Money = @"£\s*(?<v>-?[\d,]+(?:\.\d{2})?)";
    private const string BareMoney = @"(?<v>-?[\d,]+\.\d{2})";
    private const string PoundSign = "£";
    private const string SupplementaryHeading = @"Supplementary\s+Report";

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
        new(F.EngineerQualifications, K.Text,
            @"^[ \t]*[A-Z][A-Za-z'\-]+(?:[ \t]+[A-Z][A-Za-z'\-]+)+[ \t]+(?<v>[A-Z]{2,6}(?:[ \t]+[A-Z]{2,6})*)[ \t]*\n[ \t]*(?:Connexus|Exclusive)\s*Vehicle\s*Assessors"),
        new(F.Claimant, K.Text, @"^[ \t]*Client(?:/Insured)?:[ \t]*(?<v>[^\n]{2,80}?)[ \t]*$", PartyRole: "claimant"),

        // The narrative prints one combined vehicle description and never
        // separates make from model, so the printed text is recorded as
        // Ambiguous rather than split into two facts the source does not make.
        new(F.Model, K.Text, @"Vehicle:[ \t]*(?<v>[A-Z0-9][^\n]*?)[ \t]{2,}Colour:",
            Force: SourceCandidateDisposition.Ambiguous),

        new(F.Registration, K.Registration, @"Reg No:[ \t]*(?<v>[A-Z0-9]{1,4} ?[A-Z0-9]{1,4})"),
        new(F.Vin, K.Text, @"Vin No:[ \t]*(?<v>[A-HJ-NPR-Z0-9]{11,17})"),
        new(F.Mileage, K.Mileage, @"Speedo:[ \t]*(?<v>[\d,]+)", Unit: "miles"),
        new(F.MileageUnit, K.Text, @"Speedo:[ \t]*[\d,]*[ \t]*(?<v>Miles|Km)\b"),
        new(F.AccidentDate, K.Date, @"Incident:[ \t]*(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Severity, K.Text, @"Damage:[ \t]*(?<v>Light|Moderate|Heavy|Medium|Severe)\b"),
        new(F.DamageZone, K.Text,
            @"Damage:[ \t]*(?:Light|Moderate|Heavy|Medium|Severe)[ \t]+(?<v>[A-Za-z/ ]{3,40}?)[ \t]{2,}",
            Multiple: true),
        new(F.Roadworthiness, K.Text, @"Roadworthy:[ \t]*(?<v>Yes|No)\b"),
        new(F.OutcomeReason, K.Text,
            @"not roadworthy at the time of (?:our|my) inspection as a result of the damage sustained due\s+to\s+(?<v>[^\n.]{2,120})\."),
        new(F.Narrative, K.Text, @"^\s*NATURE OF (?:INCIDENT|DAMAGE)\s*$", Section: true),
        new(F.Comments, K.Text, @"^\s*ENGINEER'S COMMENTS\s*$", Section: true),
        new(F.ObservedInspectionMethod, K.Text, @"^\s*(?<v>DESKTOP ASSESSMENT)\s*$"),
        new(F.Repairer, K.Text, @"The repairers,\s*(?<v>[^,\n]{3,80}),", PartyRole: "repairer"),
        new(F.Repairer, K.Text, @"estimate has been obtained from\s*(?<v>[^,\n]{3,80}),", PartyRole: "repairer"),
        new(F.Declaration, K.Text,
            @"(?<v>In preparing this report I confirm that I understand my overriding duty to the court[^.]{0,200}\.)"),

        new(F.Gross, K.Money, @"Repair Cost:\s*" + Money + @"\s*inc\s*VAT", ReferenceRole: R.Agreed),
        new(F.Net, K.Money, @"Repair Cost:\s*" + Money + @"\s*exc\s*VAT", ReferenceRole: R.Agreed),
        new(F.LabourAmount, K.Money, @"in the sum of\s*" + Money, ReferenceRole: R.Initial),
        new(F.PaintMaterials, K.Money, @"plus\s*" + Money + @"\s*for paint and materials", ReferenceRole: R.Initial),
        new(F.SpecialistCharges, K.Money, @"plus\s*" + Money + @"\s*for specialist/sundry charges", ReferenceRole: R.Initial),
        new(F.LabourAmount, K.Money,
            @"(?:agreed an amended labour figure of|agreed a labour figure of|consider a labour charge of)\s*" + Money,
            ReferenceRole: R.Agreed),
        new(F.LabourHours, K.Number, @"labour charge is based on\s*(?<v>[\d.,]+)\s*hours",
            ReferenceRole: R.Agreed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"hours at a rate of\s*" + Money, ReferenceRole: R.Agreed, Unit: "per hour"),
        new(F.PaintMaterials, K.Money, @"cost of paint and materials (?:will|should) be limited to\s*" + Money,
            ReferenceRole: R.Agreed),
        new(F.Parts, K.Money, @"replacement parts will be approximately\s*" + Money, ReferenceRole: R.Agreed),
        new(F.SpecialistCharges, K.Money, @"specialist/sundry charges will be\s*" + Money, ReferenceRole: R.Agreed),
        new(F.VatAmount, K.Money, @"VAT liability on this repair will amount to some\s*" + Money, ReferenceRole: R.Agreed),
        new(F.Gross, K.Money, @"total repair cost of\s*" + Money + @"\s*including VAT", ReferenceRole: R.Agreed),

        new(F.ValuationGuide, K.Text, @"corresponding\s+(?<v>[A-Za-z]+(?:'s)?)\s+Guide"),
        new(F.Retail, K.Money, @"adjusted retail value[^£]{0,240}" + Money),
        new(F.Trade, K.Money, @"trade value is\s*" + Money),
        new(F.Mid, K.Money, @"mid value is\s*" + Money),
        new(F.PreAccidentValue, K.Money, @"Vehicle Value:\s*" + Money),
        new(F.PreAccidentValue, K.Money, @"pre-accident value of this particular [A-Za-z]+ at\s*" + Money),
        new(F.Reserve, K.Money, @"repair reserve of\s*" + Money),
        new(F.MinimumRepairDays, K.Number, @"take some\s*(?<v>\d+)\s*to\s*\d+\s*working days", Unit: "days"),
        new(F.MaximumRepairDays, K.Number, @"take some\s*\d+\s*to\s*(?<v>\d+)\s*working days", Unit: "days")
    ];

    /// <summary>
    /// Laird prints two layouts under one issuer: the full assessment report and
    /// the supplementary report. The Supplementary heading controls — every
    /// supplement-role rule is gated on it, which also keeps the fee invoice
    /// printed at the back of a full report out of the repair totals.
    /// </summary>
    private static readonly ThirdPartyFieldRule[] LairdRules =
    [
        new(F.ReportReference, K.Reference, @"(?<v>\d{2}-\s*\d{6,}/\d{6,})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Reference, @"(?<v>[A-Z]{2,4}(?:/[A-Z]{2,4})?/\d{4,}/\d)", ReferenceRole: "your-ref"),
        new(F.ReportDate, K.Date,
            @"Our Reference\s+Your Reference\s+Date[^\n]*\n[^£]{0,200}?(?<v>\d{1,2}(?:st|nd|rd|th)\s+[A-Za-z]{3,9}\s+\d{4})"),
        new(F.Revision, K.Text, @"^\s*(?<v>Supplementary Report)\s*$"),
        new(F.Claimant, K.Text, @"Claimant[ \t]{2,}(?<v>[^\n]{2,60}?)[ \t]*$", PartyRole: "claimant"),
        new(F.Claimant, K.Text, @"^\s*Re:\s*(?<v>[^\n]{2,60}?)\s*$", PartyRole: "claimant"),
        new(F.Make, K.Text, @"^[ \t]*Make[ \t]{2,}(?<v>[A-Za-z][A-Za-z\-]{1,20})"),
        new(F.Model, K.Text, @"^[ \t]*Model[ \t]{2,}(?<v>[^\n]{2,40}?)[ \t]*$"),
        new(F.Registration, K.Registration, @"Registration[ \t]{2,}(?<v>[A-Z0-9]{5,8})"),
        new(F.Registration, K.Registration, @"registration\s+(?<v>[A-Z]{2}\d{2} ?[A-Z]{3})\b"),
        new(F.AccidentDate, K.Date, @"Accident Date[ \t]{2,}(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.AccidentDate, K.Date, @"Road Traffic Accident on\s*(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Repairability, K.Text, @"^[ \t]*Status[ \t]{2,}(?<v>[A-Za-z ]{3,30}?)[ \t]*$"),
        new(F.Roadworthiness, K.Text, @"Legal Status[ \t]{2,}(?<v>[A-Za-z]{3,20})"),
        new(F.Severity, K.Text, @"Impact Magnitude[ \t]{2,}(?<v>[A-Za-z]{3,20})"),
        new(F.PreAccidentValue, K.Money, @"Engineer's Value[ \t]{2,}" + Money),

        new(F.LabourHours, K.Number, @"^[ \t]*Hours[ \t]{2,}(?<v>[\d.,]+)[ \t]*$",
            ReferenceRole: R.Assessed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"Hourly Rate[ \t]{2,}" + Money, ReferenceRole: R.Assessed, Unit: "per hour"),
        new(F.LabourAmount, K.Money, @"Total Labour[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.Parts, K.Money, @"^[ \t]*Parts[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.PaintMaterials, K.Money, @"Paints\s*/\s*Materials[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.SpecialistCharges, K.Money, @"Specialist\s*/\s*Other Items[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.Net, K.Money, @"Sub Total[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.VatAmount, K.Money, @"^[ \t]*VAT[ \t]{2,}" + Money, ReferenceRole: R.Assessed),
        new(F.Gross, K.Money, @"Total Estimated Cost[ \t]{2,}" + Money, ReferenceRole: R.Assessed),

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
        new(F.Gross, K.Money, @"Total:[ \t]*" + Money,
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
        new(F.Claimant, K.Text, @"Client Name[ \t]{2,}(?<v>[^\n]{2,60}?)[ \t]*$", PartyRole: "claimant"),
        new(F.VehicleLocation, K.Text, @"Inspection address[ \t]{2,}(?<v>[^\n]{2,120}?)[ \t]*$"),
        new(F.AccidentDate, K.Date, @"Date of loss[ \t]+(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.InspectionDate, K.Date, @"Date of Inspection[ \t]+(?<v>\d{1,2}/\d{1,2}/\d{4})"),
        new(F.Make, K.Text, @"^[ \t]*Make[ \t]{2,}(?<v>[A-Za-z][A-Za-z \-]{1,25}?)[ \t]{2,}"),
        new(F.Model, K.Text, @"^[ \t]*Model[ \t]{2,}(?<v>[^\n]{1,40}?)[ \t]{2,}"),
        new(F.Registration, K.Registration, @"Registration[ \t]{2,}(?<v>[A-Z0-9]{5,8})"),
        new(F.Vin, K.Text, @"V\.I\.N[ \t]{2,}(?<v>[A-HJ-NPR-Z0-9]{11,17})"),
        new(F.Mileage, K.Mileage, @"Odometer[ \t]{2,}(?<v>[\d,]+)", Unit: "miles"),
        new(F.Roadworthiness, K.Text, @"Roadworthy[ \t]{2,}(?<v>YES|NO)\b"),
        new(F.OutcomeReason, K.Text, @"Reason[ \t]{2,}(?<v>[^\n]{2,60}?)[ \t]*$"),
        new(F.Airbags, K.Text, @"Airbag[ \t]{2,}(?<v>YES|NO)\b"),
        new(F.Restraints, K.Text, @"Pre-tensioners[ \t]{2,}(?<v>YES|NO)\b"),
        new(F.Severity, K.Text, @"severity of the damage was\s*(?<v>[A-Za-z]{3,12})"),
        new(F.Tyres, K.Text, @"^[ \t]*(?<v>\d+mm[^\n]*\d+mm,?)[ \t]*$"),
        new(F.Narrative, K.Text, @"^\s*Summary\s*$", Section: true),
        new(F.Comments, K.Text, @"^\s*Comments\s*$", Section: true),
        new(F.EngineerName, K.Text, @"Consulting Motor Engineers[^\n]*\n[ \t]*(?<v>[A-Z][a-z]+ [A-Z][a-z]+)"),

        new(F.LabourHours, K.Number, @"^[ \t]*Hours[ \t]{2,}(?<v>[\d.,]+)[ \t]*$",
            ReferenceRole: R.Assessed, Unit: "hours"),
        new(F.LabourRate, K.Money, @"Hourly rate[ \t]{2,}" + BareMoney,
            ReferenceRole: R.Assessed, Unit: "per hour"),
        new(F.LabourAmount, K.Money, @"Total Labour[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Parts, K.Money, @"^[ \t]*Parts[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.PaintMaterials, K.Money, @"Paint/Materials[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.SpecialistCharges, K.Money, @"^[ \t]*Specialist[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Net, K.Money, @"Sub Total[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.VatAmount, K.Money, @"^[ \t]*VAT[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),
        new(F.Gross, K.Money, @"Total Reserve[ \t]{2,}" + BareMoney, ReferenceRole: R.Assessed),

        new(F.ValuationGuide, K.Text, @"^[ \t]*(?<v>Glass'?e?s'?)[ \t]{2,}[\d,]"),
        new(F.Trade, K.Money, @"^[ \t]*Glass'?e?s'?[ \t]{2,}(?<v>[\d,]+)[ \t]{2,}[\d,]+"),
        new(F.Retail, K.Money, @"^[ \t]*Glass'?e?s'?[ \t]{2,}[\d,]+[ \t]{2,}(?<v>[\d,]+)"),
        new(F.PreAccidentValue, K.Money, @"^[ \t]*Valuation[ \t]{2,}(?<v>[\d,]+)[ \t]*$"),
        new(F.ConditionAdjustment, K.Money, @"edition adjustment[ \t]{2,}(?<v>[\d,]+)"),
        new(F.FinalValue, K.Money, @"VEHICLE VALUE[ \t]*" + Money),
        new(F.SalvageValue, K.Money, @"Salvage value[ \t]{2,}(?<v>[\d,]+(?:\.\d{2})?)")
    ];

    private static readonly ThirdPartyFieldRule[] SPrintRules =
    [
        new(F.ReportReference, K.Reference, @"Our Ref[ \t]*:[ \t]*(?<v>\d{3,})", ReferenceRole: "our-ref"),
        new(F.ClaimReference, K.Text, @"Your Ref[ \t]*:[ \t]*(?<v>[A-Z]{2,4}[ \t]*\d{4,}[ \t]*\d)", ReferenceRole: "your-ref"),
        new(F.ReportDate, K.Date, @"Date of Report[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.InspectionDate, K.Date, @"Date of Inspection[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.AccidentDate, K.Date, @"Date of Accident[ \t]*:[ \t]*(?<v>\d{1,2} [A-Za-z]{3,9} \d{4})"),
        new(F.Claimant, K.Text, @"Insured[ \t]*:[ \t]*(?<v>[^\n]{2,60}?)[ \t]*$", PartyRole: "claimant"),
        new(F.Registration, K.Registration, @"Reg No[ \t]*:[ \t]*(?<v>[A-Z0-9]{5,8})"),
        new(F.Make, K.Text, @"Make[ \t]*:[ \t]*(?<v>[A-Z][A-Z\- ]{1,20}?)[ \t]{2,}"),
        new(F.Model, K.Text, @"Model[ \t]*:[ \t]*(?<v>[A-Z][A-Z0-9\- ]{1,25}?)[ \t]{2,}"),
        new(F.Variant, K.Text, @"Spec[ \t]*:[ \t]*(?<v>[A-Z0-9][^\n]{1,40}?)[ \t]{2,}"),
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

        new(F.Excess, K.Money, @"^[ \t]*Excess[ \t]*" + Money),
        new(F.PreAccidentValue, K.Money, @"Vehicle Market Value[ \t]*" + Money),
        new(F.Retail, K.Money, @"Glass\S{0,2}s Retail Value[ \t]*" + Money),
        new(F.Trade, K.Money, @"Glass\S{0,2}s Trade Value[ \t]*" + Money),
        new(F.FinalValue, K.Money, @"Engineers Valuation Figure[ \t]*" + Money),
        new(F.SalvageValue, K.Money, @"Salvage Value[ \t]*" + Money),
        new(F.SalvageCategory, K.Text, @"Motor Salvage Category[ \t]*:[ \t]*(?<v>[A-Z])\b")
    ];

    /// <summary>
    /// The rule table per family. John R Bell is deliberately empty: the only
    /// original in the corpus is scan-only, so no printed layout has been
    /// observed and no rule is guessed for it — its fields stay unavailable
    /// until OCR text reaches this engine (INTK-032).
    /// </summary>
    private static readonly Dictionary<ThirdPartyReportFamily, IReadOnlyList<ThirdPartyFieldRule>> Rules = new()
    {
        [ThirdPartyReportFamily.Connexus] = NarrativeRules,
        [ThirdPartyReportFamily.ExclusiveErehr] = NarrativeRules,
        [ThirdPartyReportFamily.EvaBodyshop] = NarrativeRules,
        [ThirdPartyReportFamily.Laird] = LairdRules,
        [ThirdPartyReportFamily.Montgomery] = MontgomeryRules,
        [ThirdPartyReportFamily.SPrint] = SPrintRules,
        [ThirdPartyReportFamily.JohnRBell] = []
    };

    private static readonly Dictionary<ThirdPartyReportFamily, IReadOnlyList<CompiledRule>> CompiledRules =
        Rules.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<CompiledRule>)[.. entry.Value.Select(CompiledRule.Compile)]);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>The families that carry bounded label rules today.</summary>
    public static IReadOnlyList<ThirdPartyReportFamily> ExtractableFamilies =>
        [.. Rules.Where(entry => entry.Value.Count > 0).Select(entry => entry.Key).Order()];

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
        var rows = new List<SourceFieldCandidate> { selection.Issuer };

        if (selection.Outcome != ThirdPartySelectionOutcome.Selected || selection.Family is not { } family)
        {
            return new(
                selection,
                null,
                rows,
                ThirdPartyReportValidation.Check(selection, null, rows, readResult.RequiresOcr));
        }

        var documentRole = ThirdPartyReportProfiles.DocumentRoleCode(selection.DocumentRole);
        var observed = Observe(family, pages, context, documentRole);
        rows.AddRange(observed.Values.SelectMany(field => field));
        rows.AddRange(Media(readResult, context, documentRole));

        var candidate = Project(selection, observed, rows, context);
        return new(
            selection,
            candidate,
            rows,
            ThirdPartyReportValidation.Check(selection, candidate, rows, readResult.RequiresOcr));
    }

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
    private static Dictionary<FieldKey, List<SourceFieldCandidate>> Observe(
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

                var raws = rule.Rule.Section
                    ? ThirdPartySections.Read(page.Text, rule.Value)
                    : rule.Value.Matches(page.Text).Select(match => match.Groups["v"].Value);
                foreach (var raw in raws)
                {
                    var normalized = Normalize(rule.Rule.Kind, raw);
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

        return rows;
    }

    private static ThirdPartyReportCandidate Project(
        ThirdPartyReportSelection selection,
        Dictionary<FieldKey, List<SourceFieldCandidate>> observed,
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

    private static IReadOnlyList<CompiledRule> Compiled(ThirdPartyReportFamily family) =>
        CompiledRules.TryGetValue(family, out var compiled) ? compiled : [];

    private sealed record CompiledRule(ThirdPartyFieldRule Rule, Regex Value, Regex? CoLabel)
    {
        public static CompiledRule Compile(ThirdPartyFieldRule rule) =>
            new(
                rule,
                ThirdPartyRegex.CreateMultiline(rule.Pattern),
                rule.CoLabel is null ? null : ThirdPartyRegex.CreateMultiline(rule.CoLabel));
    }

    private sealed record Observation(
        ThirdPartyFieldRule Rule,
        ThirdPartySourcePage Page,
        string Raw,
        string Normalized);

    private readonly record struct FieldKey(string Field, string ReferenceRole, string PartyRole);

    /// <summary>
    /// Reads the typed projection off the persisted rows. A conflicting field
    /// exposes no single value: both rows stay in the candidate list, and the
    /// typed fact carries the conflict rather than a chosen winner.
    /// </summary>
    private sealed class Lookup(
        Dictionary<FieldKey, List<SourceFieldCandidate>> rows,
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
            rows.TryGetValue(new(field, referenceRole, partyRole), out var found) && found.Count > 0
                ? found[0]
                : null;

        private IEnumerable<SourceFieldCandidate> All(string field) =>
            rows
                .Where(entry => entry.Key.Field == field)
                .SelectMany(entry => entry.Value)
                .Where(row => row.Disposition != SourceCandidateDisposition.Missing);
    }
}
