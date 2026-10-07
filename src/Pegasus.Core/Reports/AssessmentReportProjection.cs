using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
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
/// <see cref="Photos"/> are the operator's prepared report images
/// (<see cref="Pegasus.Core.Documents.CaseAssetPreparationPolicy.ForReport"/>):
/// Overview, Close-up, then Supporting in order, each with its rotation, crop
/// and full-page flag, joined to its confirmed custody version.
/// </para>
/// <para>
/// <see cref="Sources"/> are every other confirmed case document (any
/// semantic role), reported by their own custody name, version and hash —
/// the same provenance triple the EVA bundle's accepted-source manifest
/// already carries. This is the closest real analogue to "accepted source
/// evidence" the domain has today.
/// </para>
/// <para>
/// Repair costs come from the case's <see cref="CurrentEstimate"/> through
/// <see cref="EstimateTotals"/> — the one owner of estimate money (EXT-09,
/// FRD-11 § Estimate VAT on the rendered report). Nothing re-derives them,
/// and there is no hand-typed cost path: without a Current estimate the
/// draft fails closed naming it.
/// </para>
/// <para>
/// <see cref="ReportDate"/> is null as loaded. A report date is set only when
/// a generation freezes it, or when a preview is explicitly rendered at a
/// stated date; a persisted override wins over both.
/// </para>
/// </remarks>
public sealed record AssessmentReportProjectionInput(
    CaseAssessmentProjection Assessment,
    string OurReference,
    IReadOnlyList<string> ReportFor,
    DateOnly? ReportDate,
    IReadOnlyList<ReportImageEvidence> Photos,
    IReadOnlyList<AcceptedReportSource> Sources,
    RepairSpecificationVersion? CurrentEstimate = null,
    ReportSignatory? Signatory = null,
    ReportGuideSources? Guides = null,
    string? ValuationCommentary = null,
    IReadOnlyList<CaseReportWording>? Wording = null,
    bool SignOffEngineersOffered = false);

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
/// assessment plus its case-report inputs, or names the work still
/// outstanding. Every fact the report prints is a readiness item
/// <see cref="Prepare"/> returns (<see cref="AssessmentPolicy.EvaluatePostReviewReadiness"/>
/// plus the sign-off, Current repair spec, labour-rate and repairer VAT items
/// it shares with <see cref="CaseReportReadiness"/>), so a Case that is not ready is refused
/// with named items before anything is projected; the guards in
/// <see cref="Project"/> are invariant assertions a ready Case never reaches.
/// </summary>
public static class AssessmentReportProjection
{
    public const string RepairCostRequirement = CaseReportReadiness.CurrentEstimateRequirement;
    public const string LabourRateRequirement = CaseReportReadiness.LabourRateRequirement;

    public static AssessmentReportDraftPreparation Prepare(
        CaseAssessmentProjection assessment,
        RepairSpecificationVersion? currentEstimate = null,
        ReportSignatory? signatory = null,
        bool signOffEngineersOffered = false)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var reasons = new List<AssessmentReadinessItem>(
            AssessmentPolicy.EvaluatePostReviewReadiness(assessment));

        // These are CaseReportReadiness's own items, so the preview and
        // generation name them identically. The report's repair cost is the
        // Current repair spec's canonical total (EXT-09, FRD-11 § Estimate VAT
        // on the rendered report); there is no hand-typed cost path.
        if (CaseReportReadiness.SignOffBlocker(signatory?.IsComplete, signOffEngineersOffered) is { } signOff)
        {
            reasons.Add(signOff);
        }
        if (currentEstimate is null)
        {
            reasons.Add(CaseReportReadiness.CurrentEstimateMissing);
        }
        else
        {
            if (currentEstimate.Lines.Count == 0)
            {
                reasons.Add(CaseReportReadiness.CurrentEstimateEmpty);
            }
            if (currentEstimate.Details.HourlyRate <= 0m)
            {
                reasons.Add(CaseReportReadiness.LabourRateMissing);
            }
            if (CaseReportReadiness.RepairerVatBlocker(currentEstimate) is { } repairerVat)
            {
                reasons.Add(repairerVat);
            }
        }

        return new(reasons);
    }

    public static AssessmentReportProjectionResult Project(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var assessment = input.Assessment;
        var preparation = Prepare(
            assessment, input.CurrentEstimate, input.Signatory, input.SignOffEngineersOffered);
        if (!preparation.CanGenerate)
        {
            return new(null, preparation.Reasons);
        }
        var costs = ReportRepairCosts.For(input.CurrentEstimate!);
        var lines = input.CurrentEstimate!.Lines;
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in assessment.Fields)
        {
            fields.TryAdd(field.Path, field.Value);
        }

        var content = CaseReportReadiness.ContentOf(assessment);
        var (reportDate, reportDateOverridden) = CaseReportReadiness.ResolveReportDate(
            ParseDate(Field(fields, AssessmentVocabulary.ReportDate)),
            ParseFlag(Field(fields, AssessmentVocabulary.ReportDateOverride)) == true,
            input.ReportDate ?? throw new InvalidDataException(
                "A report date is set only when a generation or a labelled preview is rendered."));

        var owned = assessment.CaseOwned;
        var claimantName = Ready(owned.ClaimantName, "claimant name");
        var yourReference = Ready(owned.ClaimNumber, "claim reference");
        var incidentDate = Ready(owned.IncidentDate, "incident date");
        var assessedOn = Ready(owned.InspectionDate, "inspection date");
        var assessmentMethod = MapAssessmentMethod(owned.InspectionMode)
            ?? throw new InvalidDataException("A ready report is missing its inspection type.");
        var reportOutcome = MapOutcome(Field(fields, AssessmentVocabulary.Outcome)!);
        var signatory = input.Signatory!;

        var snapshot = new AssessmentReportSnapshot(
            OurReference: input.OurReference,
            YourReference: yourReference,
            ReportDate: reportDate,
            ClaimantName: claimantName,
            IncidentDate: incidentDate,
            InstructionsReceived: owned.ReceivedDate,
            Assessed: assessedOn,
            ReportFor: input.ReportFor,
            Vehicle: BuildVehicle(assessment, fields),
            Outcome: reportOutcome,
            LegalStatus: Field(fields, AssessmentVocabulary.LegalStatus)!,
            UnroadworthyReason: Field(fields, AssessmentVocabulary.UnroadworthyReason),
            ImpactSeverity: Field(fields, AssessmentVocabulary.ImpactSeverity)!,
            ImpactLocation: Field(fields, AssessmentVocabulary.ImpactLocation)!,
            AssessmentMethod: assessmentMethod!,
            LocationAddress: owned.InspectionAddress,
            EngineerValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueEngineer)) ?? 0m,
            RetailValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueRetail)) ?? 0m,
            TradeValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueTrade)) ?? 0m,
            SalvageCategory: reportOutcome == AssessmentReportOutcome.TotalLoss
                ? Field(fields, AssessmentVocabulary.SalvageCategory) : null,
            SalvageValue: reportOutcome == AssessmentReportOutcome.TotalLoss
                ? ParseMoney(Field(fields, AssessmentVocabulary.SalvageValue)) : null,
            Costs: costs,
            NewParts: LinesOf(lines, EstimateOperation.Replace),
            Repairs: LinesOf(lines, EstimateOperation.Repair, EstimateOperation.RemoveAndRefit),
            Operations: LinesOf(
                lines,
                EstimateOperation.Paint, EstimateOperation.Blend, EstimateOperation.Specialist, EstimateOperation.Other),
            Damage: BuildDamage(fields),
            SupplementaryStatement: input.CurrentEstimate?.Supplementary is { ExplainOnReport: true } supplementary
                ? supplementary.Statement
                : null,
            Settlement: BuildSettlement(reportOutcome, fields),
            HistoryCheck: Field(assessment, AssessmentVocabulary.HistoryCheck)!,
            EngineerComments: Field(assessment, AssessmentVocabulary.EngineersComments),
            Signatory: new ReportSignatory(
                signatory.PrintedName,
                string.IsNullOrWhiteSpace(signatory.Qualifications) ? null : signatory.Qualifications,
                signatory.SignatureContent.ToArray(),
                signatory.SignatureContentType),
            AgreedFee: ParseMoney(Field(assessment, AssessmentVocabulary.AgreedFee)) ?? 0m,
            FeeDescriptionLines: SplitLines(Field(assessment, AssessmentVocabulary.FeeDescriptionLines)),
            Photos: input.Photos,
            Sources: input.Sources,
            Content: content,
            Guides: input.Guides ?? ReportGuideSources.None,
            ValuationCommentary: input.ValuationCommentary,
            ReportDateOverridden: reportDateOverridden,
            // v28 P30: the Engineer's changes to the report's wording are
            // frozen with the rest of the snapshot, so a generation prints
            // their words as they stood and a later edit changes nothing
            // already issued. What a block they never touched says is
            // composed from the frozen facts beside it.
            Wording: input.Wording);

        return new(snapshot, []);
    }

    // Prepare named each fact the report prints, so reaching here without one
    // is a defect for the error page, not casework.
    private static string Ready(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"A ready report is missing its {name}.")
            : value;

    private static DateOnly Ready(DateOnly? value, string name) =>
        value ?? throw new InvalidDataException($"A ready report is missing its {name}.");

    private static string? Field(CaseAssessmentProjection assessment, string path) =>
        assessment.Field(path)?.Value;

    /// <summary>
    /// The valuation commentary the report prints when the flag is on: the
    /// Engineer's recorded commentary text when there is one, otherwise the
    /// reason recorded on the applied valuation. Both are recorded words;
    /// nothing is inferred and no placeholder is supplied (FRD-11). The reason
    /// the Case save records by itself
    /// (<see cref="ValuationCalculationPolicy.AppliedReason"/>) is a screen
    /// message, not commentary, and never prints. Null when nothing holds
    /// commentary.
    /// </summary>
    public static string? ValuationCommentaryOf(CaseAssessmentProjection assessment, string? appliedValuationReason)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var text = Field(assessment, AssessmentVocabulary.ReportValuationCommentaryText);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        return string.IsNullOrWhiteSpace(appliedValuationReason)
            || string.Equals(
                appliedValuationReason.Trim(),
                ValuationCalculationPolicy.AppliedReason,
                StringComparison.Ordinal)
            ? null
            : appliedValuationReason;
    }

    private static string? Field(IReadOnlyDictionary<string, string?> fields, string path) =>
        fields.GetValueOrDefault(path);

    /// <summary>
    /// The vehicle facts the template prints. A Case with no mileage prints
    /// the template's own word for it (DESIGN_SPEC.md § vehicle details).
    /// </summary>
    private static ReportVehicle BuildVehicle(
        CaseAssessmentProjection assessment,
        IReadOnlyDictionary<string, string?> fields)
    {
        var mileageSource = assessment.CaseOwned.MileageSource;
        var mileage = assessment.CaseOwned.Mileage;
        var mileageUnit = assessment.CaseOwned.MileageUnit ?? "miles";
        var mileageDescription = mileage is { } value
            ? $"{value.ToString("N0", CultureInfo.GetCultureInfo("en-GB"))} {mileageUnit}"
            : AssessmentReportWording.NoMileage;

        return new ReportVehicle(
            Registration: assessment.CaseOwned.Registration ?? string.Empty,
            Make: assessment.CaseOwned.Make ?? string.Empty,
            Model: assessment.CaseOwned.Model ?? string.Empty,
            Year: assessment.CaseOwned.Year ?? string.Empty,
            Condition: Field(fields, AssessmentVocabulary.VehicleCondition) ?? string.Empty,
            MileageDescription: mileageDescription,
            MileageSource: mileageSource,
            Vin: Field(fields, AssessmentVocabulary.VehicleVin),
            Engine: Field(fields, AssessmentVocabulary.VehicleEngineCc),
            Fuel: Field(fields, AssessmentVocabulary.VehicleFuel));
    }

    private static ReportDamage BuildDamage(IReadOnlyDictionary<string, string?> fields)
    {
        var impacts = AssessmentPolicy.ParseImpacts(Field(fields, AssessmentVocabulary.DamageImpacts))
            .Select(impact => new ReportImpact(impact.Areas, impact.Severity, impact.Disc))
            .ToArray();
        return new(
            impacts,
            Field(fields, AssessmentVocabulary.DamageUnrelated),
            DamagePlanGeometry.ProfileFor(Field(fields, AssessmentVocabulary.VehicleType)));
    }

    /// <summary>
    /// The agreed contract repair sum, which only a contract repair carries.
    /// Prepare named a missing sum, so a contract repair that reaches here
    /// without a positive one is a defect for the error page.
    /// </summary>
    private static ReportSettlement BuildSettlement(
        AssessmentReportOutcome outcome,
        IReadOnlyDictionary<string, string?> fields)
    {
        if (outcome != AssessmentReportOutcome.ContractRepair)
        {
            return new ReportSettlement();
        }
        var agreed = ParseMoney(Field(fields, AssessmentVocabulary.SettlementContractSum));
        if (agreed is not > 0m)
        {
            throw new InvalidDataException("A ready contract repair report is missing its agreed contract sum.");
        }
        return new ReportSettlement(agreed);
    }

    /// <summary>
    /// Groups the Current estimate's line descriptions for the report's
    /// parts/repairs/operations lists. The lines are the Current accepted
    /// specification's — Use estimate is the acceptance — so this only has to
    /// group by operation, as the Case page's lists do, and drop blank
    /// descriptions. Every line type has an operation, so none is dropped.
    /// </summary>
    private static string[] LinesOf(
        IReadOnlyList<CaseEstimateLineRecord> lines, params ReadOnlySpan<EstimateOperation> operations)
    {
        var operationSet = new HashSet<EstimateOperation>(operations.ToArray());
        return lines
            .Where(line => operationSet.Contains(EstimateOperations.FromLineType(line.Type))
                && !string.IsNullOrWhiteSpace(line.Description))
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

    /// <summary>
    /// Persisted dates are invariant <c>yyyy-MM-dd</c>. Parsing them
    /// under the ambient culture reads a Buddhist- or Hijri-calendar year on a
    /// th-TH or ar-SA workstation, so the culture is always stated.
    /// </summary>
    private static DateOnly? ParseDate(string? value) =>
        value is not null && DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static bool? ParseFlag(string? value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

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
        Guid caseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken = default);
}

/// <summary>
/// The read-only preparation a control renders from: assessment/report work is
/// complete, or the exact remaining reasons, including every Case fact the
/// report prints.
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
    RenderedReportArtifact? Draft,
    IReadOnlyList<AssessmentReadinessItem> Reasons);

/// <summary>
/// The reachable operator entry point: loads a case's report
/// inputs, projects them, and renders the draft only when every requirement
/// is met. Authorisation is inherited from the composed
/// <see cref="IAssessmentReportProjectionSource"/> (the same
/// <c>StaffAuthorization</c> check the case-detail query already performs) —
/// nothing new is invented here.
/// </summary>
public sealed class GenerateCaseAssessmentReportDraft(
    IGetAssessmentAccess getAssessmentAccess,
    IAssessmentReportProjectionSource source,
    GenerateAssessmentReportDraft generate,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Renders a labelled preview of the working snapshot for exactly the
    /// requested kind. Nothing is persisted: no generation, no artifact, no
    /// custody object and no Sent claim. The preview's report date is today's
    /// unless the Case records an override — a generation is what freezes one.
    /// </summary>
    public async Task<GenerateCaseAssessmentReportDraftResult> ExecuteAsync(
        Guid caseId,
        ActionActor actor,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default)
    {
        var access = await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken);
        // Native report preview uses the same lifecycle action gate as the
        // other Engineer sections; no external export is a prerequisite.
        if (access is null || !access.CanOpen)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var input = await source.GetAsync(caseId, actor, CaseWorkSelector.Current, cancellationToken);
        if (input is null)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var projected = AssessmentReportProjection.Project(input with
        {
            ReportDate = LondonCalendar.DateAt(timeProvider.GetUtcNow()),
        });
        if (!projected.IsReady)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotReady, null, projected.Reasons);
        }

        var draft = await generate.ExecuteAsync(projected.Snapshot!, kind, cancellationToken);
        return new(GenerateCaseAssessmentReportDraftOutcome.Generated, draft, []);
    }
}
