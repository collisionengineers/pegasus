using System.Security.Cryptography;
using System.Globalization;

namespace Pegasus.Core.Reports;

public static class AssessmentReportContract
{
    public const string TemplateVersion = "rendererref1-v1";
    public const string VatNumber = "262 0937 10";
    public const string AccountName = "Collision Engineers Ltd";
    public const string BankName = "Lloyds Bank";
    public const string SortCode = "30-12-80";
    public const string AccountNumber = "50858868";
    public const string RemittanceEmail = "accounts@collisionengineers.co.uk";
    public const string FeeTerms = "As per this agreement, following which we reserve the right to claim statutory interest at 8% above the Bank of England reference rate in force on the date the debt becomes overdue and at any subsequent rate where the reference rate changes and the debt remains unpaid, in accordance with the Late Payment of Commercial Debts (Interest) Act 1998 as amended and supplemented by the Late Payment of Commercial Debts Regulations 2002. Payment is due in full within 89 days from the date of this report unless otherwise stated. In addition, for unpaid debts up to £999.99 we are allowed to claim compensation of £40.00.";
    public const string AdditionalFeeTerms = "Any requests for addendum reports or letters, including those required for clarification, plus Counsel, Court or other meetings, will be subject to a further charge and subject to Civil Procedure Rule 35.6. The instructing party confirm to be liable for the charges of this report and any subsequent addendum reports on acceptance of this report by electronic mail. If you do not so wish to be bound by these terms you must reject the report and confirm so immediately.";
    public const string StatementOfTruth1 = "I declare that I understand my duty in providing this report to the court and I confirm that I have complied with that duty. I understand that this duty overrides any other obligation. The report is based upon instructions received.";
    public const string StatementOfTruth2 = "I confirm that I have made clear which facts and matters referred to in this report are within my own knowledge and which are not. Those that are within my own knowledge I confirm to be true. The opinions I have expressed represent my true and complete professional opinion on the matters to which they refer.";
    public const string StatementOfTruth3 = "We have used Glass's Evaluator to assist with the valuation of the vehicle and Thatcham and/or manufacturer's data to compile the repair specification. Parts prices are subject to fluctuation and further damage may be found upon dismantling the vehicle. Our valuation is based on the mileage information provided and assuming that the vehicle has a valid MOT certificate (where applicable) to support such.";
    public const string StatementOfTruth4 = "We appreciate your instructions and enclose our fee note for your kind attention, which we confirm remains payable irrespective of the outcome of this case. Please ensure this is passed to your accounts department.";
}

public enum AssessmentReportOutcome
{
    TotalLoss,
    Repairable,
    CashInLieu,
    ContractRepair,
}

public sealed record AcceptedReportSource(string Name, string Version, string Sha256)
{
    public void Validate()
    {
        Required(Name, nameof(Name));
        Required(Version, nameof(Version));
        if (Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit))
        {
            throw new ReportRenderRejectedException("Every accepted source requires a SHA-256 hash.");
        }
    }

    internal static void Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReportRenderRejectedException($"{name} is required.");
        }
    }
}

public sealed record ReportVehicle(
    string Registration,
    string Make,
    string Model,
    string Year,
    string VehicleType,
    string Condition,
    string MileageDescription,
    string MileageSource = "tbc",
    string? Vin = null,
    string? Engine = null,
    string? Fuel = null);

public sealed record ReportImageEvidence(
    string CustodyReference,
    string ContentType,
    byte[] Content,
    string Sha256)
{
    public void Validate()
    {
        AcceptedReportSource.Required(CustodyReference, nameof(CustodyReference));
        if (ContentType is not ("image/jpeg" or "image/png" or "image/webp") || Content.Length == 0)
        {
            throw new ReportRenderRejectedException("Every report image requires accepted image bytes and content type.");
        }
        var actual = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Content));
        if (!actual.Equals(Sha256, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException("A report image did not match its custody hash.");
        }
    }
}

/// <summary>
/// The report's repair-cost block. When the case has a Current estimate the
/// figures, including <see cref="Vat"/>, come from
/// <c>Pegasus.Core.Assessment.EstimateTotals</c> through
/// <see cref="VatOverride"/> (FRD-11 § Estimate VAT on the rendered report);
/// the built-in repairer-VAT-registered rule below applies only when no
/// Current estimate exists.
/// </summary>
public sealed record ReportRepairCosts(
    decimal LabourHours,
    decimal HourlyRate,
    decimal Parts,
    decimal PaintMaterials,
    decimal SpecialistOther,
    bool RepairerVatRegistered,
    decimal? VatOverride = null)
{
    public decimal Labour => LabourHours * HourlyRate;
    public decimal Subtotal => Labour + Parts + PaintMaterials + SpecialistOther;
    public decimal Vat => VatOverride ?? decimal.Round(
        (RepairerVatRegistered ? Subtotal : Parts + PaintMaterials) * 0.20m,
        2,
        MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Vat;
}

public sealed record ReportEngineer(
    string Name,
    string Qualifications,
    string SignatureKey);

public sealed record AssessmentReportPresentation(
    string Title,
    string Badge,
    string SettlementHeading,
    string SettlementLabel,
    string SettlementText,
    decimal? RecommendedSettlement);

public sealed record AssessmentReportSnapshot(
    string OurReference,
    string YourReference,
    DateOnly ReportDate,
    string ClaimantName,
    DateOnly IncidentDate,
    DateOnly InstructionsReceived,
    DateOnly Assessed,
    IReadOnlyList<string> ReportFor,
    ReportVehicle Vehicle,
    AssessmentReportOutcome Outcome,
    string LegalStatus,
    string? UnroadworthyReason,
    string ImpactSeverity,
    string ImpactLocation,
    string AssessmentMethod,
    string? LocationAddress,
    decimal EngineerValue,
    decimal RetailValue,
    decimal TradeValue,
    string? SalvageCategory,
    decimal? SalvageValue,
    ReportRepairCosts Costs,
    IReadOnlyList<string> NewParts,
    IReadOnlyList<string> Repairs,
    IReadOnlyList<string> Operations,
    string HistoryCheck,
    string? EngineerComments,
    ReportEngineer Engineer,
    decimal AgreedFee,
    IReadOnlyList<string> FeeDescriptionLines,
    IReadOnlyList<ReportImageEvidence> Photos,
    IReadOnlyList<AcceptedReportSource> Sources,
    string PayloadVersion = AssessmentReportContract.TemplateVersion)
{
    private static readonly Dictionary<string, (string Name, string Qualifications)> AcceptedEngineers =
        new(StringComparer.Ordinal)
        {
            ["andy_patterson"] = ("A Patterson", "M.Inst.IAEA"),
        };

    /// <summary>
    /// The single lookup against the accepted-signatory list, exposed so a
    /// caller can report an unrecognized engineer signature as a named
    /// readiness gap before attempting to build a snapshot, rather than
    /// duplicating this table (one list per concept).
    /// </summary>
    public static bool TryResolveAcceptedEngineer(
        string signatureKey, out string name, out string qualifications)
    {
        if (AcceptedEngineers.TryGetValue(signatureKey, out var accepted))
        {
            (name, qualifications) = accepted;
            return true;
        }
        name = string.Empty;
        qualifications = string.Empty;
        return false;
    }

    public void Validate()
    {
        AcceptedReportSource.Required(OurReference, nameof(OurReference));
        AcceptedReportSource.Required(YourReference, nameof(YourReference));
        AcceptedReportSource.Required(ClaimantName, nameof(ClaimantName));
        AcceptedReportSource.Required(Vehicle.Registration, nameof(Vehicle.Registration));
        AcceptedReportSource.Required(HistoryCheck, nameof(HistoryCheck));
        AcceptedReportSource.Required(Engineer.Name, nameof(Engineer.Name));
        AcceptedReportSource.Required(Engineer.Qualifications, nameof(Engineer.Qualifications));
        AcceptedReportSource.Required(PayloadVersion, nameof(PayloadVersion));
        if (ReportFor.Count == 0 || Photos.Count == 0 || Sources.Count == 0)
        {
            throw new ReportRenderRejectedException("Report addressee, photo custody and accepted source evidence are required.");
        }
        if (Costs.LabourHours < 0 || Costs.HourlyRate <= 0 || Costs.Parts < 0 ||
            Costs.PaintMaterials < 0 || Costs.SpecialistOther < 0 || EngineerValue <= 0 || AgreedFee <= 0)
        {
            throw new ReportRenderRejectedException("Accepted report amounts are incomplete or invalid.");
        }
        if (Outcome == AssessmentReportOutcome.TotalLoss &&
            (!string.Equals(SalvageCategory, "S", StringComparison.Ordinal) || SalvageValue is null or < 0))
        {
            throw new ReportRenderRejectedException("The active total-loss report requires accepted Category S wording and salvage value.");
        }
        if (LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(UnroadworthyReason))
        {
            throw new ReportRenderRejectedException("An accepted unroadworthy reason is required.");
        }
        if (ReportFor.Any(string.IsNullOrWhiteSpace))
        {
            throw new ReportRenderRejectedException("Report inputs cannot contain blank entries.");
        }
        foreach (var source in Sources)
        {
            source.Validate();
        }
        foreach (var photo in Photos)
        {
            photo.Validate();
        }
        if (AssessmentMethod is not ("image_based" or "physical") ||
            AssessmentMethod == "physical" && string.IsNullOrWhiteSpace(LocationAddress))
        {
            throw new ReportRenderRejectedException("The accepted assessment method/location is incomplete.");
        }
        AcceptedReportSource.Required(ImpactSeverity, nameof(ImpactSeverity));
        AcceptedReportSource.Required(ImpactLocation, nameof(ImpactLocation));
        if (!PayloadVersion.Equals(AssessmentReportContract.TemplateVersion, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException($"Unsupported payload version '{PayloadVersion}'.");
        }
        if (!AcceptedEngineers.TryGetValue(Engineer.SignatureKey, out var accepted) ||
            !accepted.Name.Equals(Engineer.Name, StringComparison.Ordinal) ||
            !accepted.Qualifications.Equals(Engineer.Qualifications, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException(
                "Engineer name, qualifications and signature do not match an accepted rendererref1 identity.");
        }
    }

    public AssessmentReportPresentation Presentation() => Outcome switch
    {
        AssessmentReportOutcome.TotalLoss => new(
            "TOTAL LOSS REPORT",
            $"TOTAL LOSS — CATEGORY {SalvageCategory}",
            "Settlement",
            "Recommended equitable settlement (pre-accident value less salvage)",
            $"We consider that an equitable settlement would be {Money(EngineerValue - SalvageValue!.Value)}, which represents the pre-accident engineer value of the vehicle of {Money(EngineerValue)} less the value of the salvage of {Money(SalvageValue.Value)}.",
            EngineerValue - SalvageValue!.Value),
        AssessmentReportOutcome.Repairable => new(
            "REPAIRABLE REPORT", "REPAIRABLE",
            "Settlement", "Recommended settlement (calculated repair cost)",
            $"This vehicle is considered a repairable proposition and we have calculated a repair cost of {Money(Costs.Total)}.",
            Costs.Total),
        AssessmentReportOutcome.CashInLieu => new(
            "CASH IN LIEU REPORT", "CASH IN LIEU",
            "Settlement", "Cash in lieu settlement",
            $"We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of {Money(Costs.Total)}.",
            Costs.Total),
        AssessmentReportOutcome.ContractRepair => new(
            "CONTRACT REPAIR REPORT", "CONTRACT REPAIR",
            "Contract Repair", "Agreed contract repair",
            $"A contract repair has been agreed for the sum of {Money(Costs.Total)} including VAT. Costs cannot increase above this figure.",
            Costs.Total),
        _ => throw new ReportRenderRejectedException("Unsupported assessment outcome."),
    };

    public decimal FeeNet => AgreedFee;
    public decimal FeeVat => decimal.Round(FeeNet * 0.20m, 2, MidpointRounding.AwayFromZero);
    public decimal FeeTotal => FeeNet + FeeVat;

    private static string Money(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
}

public sealed record RenderedReportArtifact(
    string SuggestedFileName,
    byte[] Pdf,
    int PageCount,
    string Sha256,
    string TemplateVersion,
    string EngineVersion);

public sealed record AssessmentReportDraft(
    RenderedReportArtifact Assessment,
    RenderedReportArtifact FeeNote);

public interface IAssessmentReportRenderer
{
    Task<AssessmentReportDraft> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public sealed class GenerateAssessmentReportDraft(IAssessmentReportRenderer renderer)
{
    public async Task<AssessmentReportDraft> ExecuteAsync(
        AssessmentReportSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        var result = await renderer.RenderAsync(snapshot, cancellationToken).ConfigureAwait(false);
        foreach (var artifact in new[] { result.Assessment, result.FeeNote })
        {
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf));
            if (!actualHash.Equals(artifact.Sha256, StringComparison.Ordinal))
            {
                throw new ReportRenderRejectedException("The renderer returned an artifact with mismatched provenance.");
            }
        }
        return result;
    }
}

public sealed class ReportRenderRejectedException(string message) : Exception(message);
