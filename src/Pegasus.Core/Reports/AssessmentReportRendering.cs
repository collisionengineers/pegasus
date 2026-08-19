using System.Security.Cryptography;

namespace Pegasus.Core.Reports;

public static class AssessmentReportContract
{
    public const string TemplateVersion = "rendererref1-v1";
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
    string MileageDescription);

public sealed record ReportRepairCosts(
    decimal LabourHours,
    decimal HourlyRate,
    decimal Parts,
    decimal PaintMaterials,
    decimal SpecialistOther,
    bool RepairerVatRegistered)
{
    public decimal Labour => LabourHours * HourlyRate;
    public decimal Subtotal => Labour + Parts + PaintMaterials + SpecialistOther;
    public decimal Vat => decimal.Round(
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
    string SettlementText,
    decimal? RecommendedSettlement);

public sealed record AssessmentReportSnapshot(
    string OurReference,
    string YourReference,
    DateOnly ReportDate,
    string ClaimantName,
    DateOnly IncidentDate,
    IReadOnlyList<string> ReportFor,
    ReportVehicle Vehicle,
    AssessmentReportOutcome Outcome,
    string LegalStatus,
    string? UnroadworthyReason,
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
    IReadOnlyList<string> PhotoCustodyReferences,
    IReadOnlyList<AcceptedReportSource> Sources,
    string PayloadVersion = AssessmentReportContract.TemplateVersion)
{
    private static readonly Dictionary<string, (string Name, string Qualifications)> AcceptedEngineers =
        new(StringComparer.Ordinal)
        {
            ["andy_patterson"] = ("A Patterson", "M.Inst.IAEA"),
        };

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
        if (ReportFor.Count == 0 || PhotoCustodyReferences.Count == 0 || Sources.Count == 0)
        {
            throw new ReportRenderRejectedException("Report addressee, photo custody and accepted source evidence are required.");
        }
        if (Costs.LabourHours < 0 || Costs.HourlyRate <= 0 || Costs.Parts < 0 ||
            Costs.PaintMaterials < 0 || Costs.SpecialistOther < 0 || EngineerValue <= 0 || AgreedFee <= 0)
        {
            throw new ReportRenderRejectedException("Accepted report amounts are incomplete or invalid.");
        }
        if (Outcome == AssessmentReportOutcome.TotalLoss &&
            (string.IsNullOrWhiteSpace(SalvageCategory) || SalvageValue is null or < 0))
        {
            throw new ReportRenderRejectedException("Total-loss reports require accepted salvage category and value.");
        }
        if (LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(UnroadworthyReason))
        {
            throw new ReportRenderRejectedException("An accepted unroadworthy reason is required.");
        }
        if (ReportFor.Any(string.IsNullOrWhiteSpace) || PhotoCustodyReferences.Any(string.IsNullOrWhiteSpace))
        {
            throw new ReportRenderRejectedException("Report inputs cannot contain blank entries.");
        }
        foreach (var source in Sources)
        {
            source.Validate();
        }
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
            "The recommended settlement is the accepted Engineer value less salvage.",
            EngineerValue - SalvageValue!.Value),
        AssessmentReportOutcome.Repairable => new(
            "REPAIRABLE REPORT", "REPAIRABLE",
            "The recommended settlement is the calculated repair cost for the Engineer's repairable finding.",
            Costs.Total),
        AssessmentReportOutcome.CashInLieu => new(
            "CASH IN LIEU REPORT", "CASH IN LIEU",
            "The recommended cash-in-lieu settlement is the calculated repair cost.",
            Costs.Total),
        AssessmentReportOutcome.ContractRepair => new(
            "CONTRACT REPAIR REPORT", "CONTRACT REPAIR",
            "The agreed contract-repair cap is the calculated VAT-inclusive repair total and cannot increase.",
            Costs.Total),
        _ => throw new ReportRenderRejectedException("Unsupported assessment outcome."),
    };
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
