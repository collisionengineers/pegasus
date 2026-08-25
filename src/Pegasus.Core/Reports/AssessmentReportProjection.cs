using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// The case-level facts <see cref="AssessmentReportSnapshot"/> needs beyond
/// the assessment record itself: the accepted case's own identity and
/// addressee, plus the custody-confirmed evidence a report draws on. Every
/// field here is loaded from an existing accepted source (<see
/// cref="Assessment.CaseAssessmentProjection"/>, the case-detail projection,
/// and confirmed case-document custody) — nothing is synthesized.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Photos"/> are confirmed <c>Image</c>-role case documents,
/// following the same custody query the EVA hand-off bundle already uses
/// (<see cref="Pegasus.Core.Eva.EvaBundleImage"/>): current, not logically
/// removed, custody-confirmed. UI-15's photograph curation (which photo, what
/// order) is explicitly deferred — see the "Report images" section of
/// <c>src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml</c> — so every
/// confirmed image on the case is offered in custody (occurrence) order
/// rather than an operator-curated subset.
/// </para>
/// <para>
/// <see cref="Sources"/> are every other confirmed case document (any
/// semantic role), reported by their own custody name, version and hash —
/// the same provenance triple the EVA bundle's accepted-source manifest
/// already carries. This is the closest real analogue to "accepted source
/// evidence" the domain has today.
/// </para>
/// <para>
/// <see cref="Costs"/> is deliberately nullable and supplied by the caller,
/// not derived here. <c>AssessmentPolicy</c> already documents that estimate
/// derivation (totals, worklists) is "deliberately absent until its formulas
/// hold accepted authority (EXT-09, open decision D2)", and the assessment
/// screen's own rate-card section states the labour/paint rate is "published
/// reference data with their own dates and caveat, not a Pegasus tariff" — no
/// numeric hourly rate or paint-materials figure is accepted anywhere in the
/// domain today. Inventing one here would fabricate a legal-report money
/// figure, which is exactly what this projection must not do. Until EXT-09
/// lands, every production caller passes <c>Costs: null</c> and the report
/// draft fails closed with that reason named.
/// </para>
/// </remarks>
public sealed record AssessmentReportProjectionInput(
    CaseAssessmentProjection Assessment,
    string? ClaimantName,
    string OurReference,
    string? YourReference,
    IReadOnlyList<string> ReportFor,
    DateOnly ReportDate,
    IReadOnlyList<ReportImageEvidence> Photos,
    IReadOnlyList<AcceptedReportSource> Sources,
    ReportRepairCosts? Costs);

/// <summary>
/// Either a snapshot ready to render, or the enumerated reasons it is not —
/// never both, and never a snapshot the caller has to re-validate.
/// </summary>
public sealed record AssessmentReportProjectionResult(
    AssessmentReportSnapshot? Snapshot,
    IReadOnlyList<AssessmentReadinessItem> Reasons)
{
    public bool IsReady => Snapshot is not null;
}

/// <summary>
/// Builds an <see cref="AssessmentReportSnapshot"/> from an accepted
/// assessment plus its case-report inputs, or names the assessment/report work
/// still outstanding. Case identity, instruction and image completeness are
/// not re-decided here: entry to Review already proved those lifecycle gates.
/// If persisted Review data later violates one of those invariants, generation
/// fails at the immutable snapshot boundary instead of presenting the defect as
/// ordinary assessment work.
/// </summary>
public static class AssessmentReportProjection
{
    public const string RepairCostRequirement = "Repair cost figures";

    public static AssessmentReportDraftPreparation Prepare(
        CaseAssessmentProjection assessment,
        ReportRepairCosts? costs)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var reasons = new List<AssessmentReadinessItem>(
            AssessmentPolicy.EvaluatePostReviewReadiness(assessment));

        void Require(bool ok, string requirement, string source, string whyOutstanding, string howToResolve)
        {
            if (!ok)
            {
                reasons.Add(new(requirement, source, whyOutstanding, howToResolve));
            }
        }

        var engineerSignature = Field(assessment, AssessmentVocabulary.EngineerSignature);
        var engineerName = Field(assessment, AssessmentVocabulary.EngineerName);
        var engineerQualifications = Field(assessment, AssessmentVocabulary.EngineerQualifications);
        if (engineerSignature is not null)
        {
            var accepted = AssessmentReportSnapshot.TryResolveAcceptedEngineer(
                    engineerSignature, out var acceptedName, out var acceptedQualifications)
                && string.Equals(acceptedName, engineerName, StringComparison.Ordinal)
                && string.Equals(acceptedQualifications, engineerQualifications, StringComparison.Ordinal);
            Require(
                accepted,
                "Accepted engineer signature", "Assessment record",
                "The recorded engineer name, qualifications and signature do not match an accepted signatory.",
                "Record the exact accepted engineer name, qualifications and signature.");
        }

        // The rate-card / paint-materials formula the report needs has no
        // accepted authority anywhere in the domain yet (EXT-09, open
        // decision D2) — see the remarks on AssessmentReportProjectionInput.
        // A production caller never supplies Costs, so this fires for every
        // case today; that is the honest state of the capability, not a bug.
        Require(
            costs is not null,
            RepairCostRequirement, "Estimate lines and rate card",
            "No accepted formula exists yet to convert recorded estimate lines and the "
                + "chosen rate card into a labour rate and repair cost (EXT-09, open decision D2).",
            "This becomes available once EXT-09's estimate-derivation formula is accepted.");

        return new(reasons);
    }

    public static AssessmentReportProjectionResult Project(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var assessment = input.Assessment;
        var preparation = Prepare(assessment, input.Costs);
        if (!preparation.CanGenerate)
        {
            return new(null, preparation.Reasons);
        }

        var claimantName = RequiredReviewValue(input.ClaimantName, "claimant name");
        var yourReference = RequiredReviewValue(input.YourReference, "claim number");
        var incidentDate = RequiredReviewDate(assessment.CaseOwned.IncidentDate, "incident date");
        var instructionDate = RequiredReviewDate(
            assessment.CaseOwned.InstructionDate,
            "instruction date");
        var assessmentMethod = MapAssessmentMethod(assessment.CaseOwned.InspectionMode)
            ?? throw new InvalidDataException(
                "A Review case is missing its accepted inspection method.");
        var engineerSignature = Field(assessment, AssessmentVocabulary.EngineerSignature)!;
        var engineerName = Field(assessment, AssessmentVocabulary.EngineerName)!;
        var engineerQualifications = Field(
            assessment,
            AssessmentVocabulary.EngineerQualifications)!;

        var snapshot = new AssessmentReportSnapshot(
            OurReference: input.OurReference,
            YourReference: yourReference,
            ReportDate: input.ReportDate,
            ClaimantName: claimantName,
            IncidentDate: incidentDate,
            InstructionsReceived: instructionDate,
            Assessed: ParseDate(Field(assessment, AssessmentVocabulary.IncidentAssessed)) ?? default,
            ReportFor: input.ReportFor,
            Vehicle: BuildVehicle(assessment),
            Outcome: MapOutcome(Field(assessment, AssessmentVocabulary.Outcome)!),
            LegalStatus: Field(assessment, AssessmentVocabulary.LegalStatus)!,
            UnroadworthyReason: Field(assessment, AssessmentVocabulary.UnroadworthyReason),
            ImpactSeverity: Field(assessment, AssessmentVocabulary.ImpactSeverity)!,
            ImpactLocation: Field(assessment, AssessmentVocabulary.ImpactLocation)!,
            AssessmentMethod: assessmentMethod!,
            LocationAddress: assessment.CaseOwned.InspectionAddress,
            EngineerValue: ParseMoney(Field(assessment, AssessmentVocabulary.ValueEngineer)) ?? 0m,
            RetailValue: ParseMoney(Field(assessment, AssessmentVocabulary.ValueRetail)) ?? 0m,
            TradeValue: ParseMoney(Field(assessment, AssessmentVocabulary.ValueTrade)) ?? 0m,
            SalvageCategory: Field(assessment, AssessmentVocabulary.SalvageCategory),
            SalvageValue: ParseMoney(Field(assessment, AssessmentVocabulary.SalvageValue)),
            Costs: input.Costs!,
            NewParts: LinesOfType(assessment, "new_part"),
            Repairs: LinesOfType(assessment, "repair"),
            Operations: LinesOfType(
                assessment,
                "check_labour", "paint_new", "paint_repair", "paint_blend", "paint_prep",
                "specialist_fixed", "specialist_wu"),
            HistoryCheck: Field(assessment, AssessmentVocabulary.HistoryCheck)!,
            EngineerComments: Field(assessment, AssessmentVocabulary.EngineersComments),
            Engineer: new ReportEngineer(engineerName!, engineerQualifications!, engineerSignature!),
            AgreedFee: ParseMoney(Field(assessment, AssessmentVocabulary.AgreedFee)) ?? 0m,
            FeeDescriptionLines: SplitLines(Field(assessment, AssessmentVocabulary.FeeDescriptionLines)),
            Photos: input.Photos,
            Sources: input.Sources);

        return new(snapshot, []);
    }

    private static string RequiredReviewValue(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"A Review case is missing its accepted {name}.")
            : value;

    private static DateOnly RequiredReviewDate(DateOnly? value, string name) =>
        value ?? throw new InvalidDataException($"A Review case is missing its accepted {name}.");

    private static string? Field(CaseAssessmentProjection assessment, string path) =>
        assessment.Field(path)?.Value;

    private static ReportVehicle BuildVehicle(CaseAssessmentProjection assessment)
    {
        var mileageSource = Field(assessment, AssessmentVocabulary.VehicleMileageSource) ?? "tbc";
        var mileage = assessment.CaseOwned.Mileage;
        var mileageUnit = assessment.CaseOwned.MileageUnit ?? "miles";
        var mileageDescription = mileage is { } value
            ? $"{value:N0} {mileageUnit}"
            : "To be confirmed";

        return new ReportVehicle(
            Registration: assessment.CaseOwned.Registration ?? string.Empty,
            Make: assessment.CaseOwned.Make ?? string.Empty,
            Model: assessment.CaseOwned.Model ?? string.Empty,
            Year: Field(assessment, AssessmentVocabulary.VehicleYear) ?? string.Empty,
            VehicleType: Field(assessment, AssessmentVocabulary.VehicleType) ?? string.Empty,
            Condition: Field(assessment, AssessmentVocabulary.VehicleCondition) ?? string.Empty,
            MileageDescription: mileageDescription,
            MileageSource: mileageSource,
            Vin: Field(assessment, AssessmentVocabulary.VehicleVin),
            Engine: Field(assessment, AssessmentVocabulary.VehicleEngineCc),
            Fuel: Field(assessment, AssessmentVocabulary.VehicleFuel));
    }

    /// <summary>
    /// Groups confirmed line descriptions for the report's parts/repairs/
    /// operations lists. Every estimate line is already confirmed by the
    /// time this runs — <see cref="AssessmentPolicy.EvaluatePostReviewReadiness"/>
    /// blocks the whole draft on the first unconfirmed line, of any type —
    /// so this only has to group by type and drop blank descriptions.
    /// </summary>
    private static string[] LinesOfType(
        CaseAssessmentProjection assessment, params ReadOnlySpan<string> types)
    {
        var typeSet = new HashSet<string>(types.ToArray(), StringComparer.Ordinal);
        return assessment.EstimateLines
            .Where(line => typeSet.Contains(line.Type) && !string.IsNullOrWhiteSpace(line.Description))
            .OrderBy(line => line.Position)
            .Select(line => line.Description!)
            .ToArray();
    }

    private static string? MapAssessmentMethod(string? inspectionMode) => inspectionMode switch
    {
        "PhysicalAddress" => "physical",
        "ImageBasedAssessment" => "image_based",
        _ => null,
    };

    private static AssessmentReportOutcome MapOutcome(string value) => value switch
    {
        "total_loss" => AssessmentReportOutcome.TotalLoss,
        "repairable" => AssessmentReportOutcome.Repairable,
        "cash_in_lieu" => AssessmentReportOutcome.CashInLieu,
        "contract_repair" => AssessmentReportOutcome.ContractRepair,
        _ => throw new InvalidOperationException($"Unrecognized assessment outcome '{value}'."),
    };

    private static decimal? ParseMoney(string? value) =>
        value is not null
            && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static DateOnly? ParseDate(string? value) =>
        value is not null && DateOnly.TryParseExact(value, "yyyy-MM-dd", out var parsed)
            ? parsed
            : null;

    private static string[] SplitLines(string? value) =>
        value is null
            ? []
            : value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// The single Core-owned port for everything a report draft needs beyond the
/// assessment record: the case's own identity/addressee and its
/// custody-confirmed photograph and source evidence. Infrastructure supplies
/// it by composing the same accepted queries (case detail, assessment,
/// document custody) the rest of the app already uses — no new persistence.
/// </summary>
public interface IAssessmentReportProjectionSource
{
    Task<AssessmentReportProjectionInput?> GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// The read-only preparation a control renders from: assessment/report work is
/// complete, or the exact remaining reasons. Review-entry requirements are not
/// repeated here.
/// </summary>
public sealed record AssessmentReportDraftPreparation(IReadOnlyList<AssessmentReadinessItem> Reasons)
{
    public bool CanGenerate => Reasons.Count == 0;
}

public enum GenerateCaseAssessmentReportDraftOutcome
{
    Generated,
    NotReady,
    NotFound,
}

public sealed record GenerateCaseAssessmentReportDraftResult(
    GenerateCaseAssessmentReportDraftOutcome Outcome,
    AssessmentReportDraft? Draft,
    IReadOnlyList<AssessmentReadinessItem> Reasons);

/// <summary>
/// The reachable operator entry point (DELIV-012): loads a case's report
/// inputs, projects them, and renders the draft only when every requirement
/// is met. Authorisation is inherited from the composed
/// <see cref="IAssessmentReportProjectionSource"/> (the same
/// <c>StaffAuthorization</c> check the case-detail query already performs) —
/// nothing new is invented here.
/// </summary>
public sealed class GenerateCaseAssessmentReportDraft(
    IGetAssessmentAccess getAssessmentAccess,
    IAssessmentReportProjectionSource source,
    GenerateAssessmentReportDraft generate)
{
    public async Task<GenerateCaseAssessmentReportDraftResult> ExecuteAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken = default)
    {
        var access = await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken);
        if (access?.CanOpen != true)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var input = await source.GetAsync(caseId, actor, cancellationToken);
        if (input is null)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var projected = AssessmentReportProjection.Project(input);
        if (!projected.IsReady)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotReady, null, projected.Reasons);
        }

        var draft = await generate.ExecuteAsync(projected.Snapshot!, cancellationToken);
        return new(GenerateCaseAssessmentReportDraftOutcome.Generated, draft, []);
    }
}
