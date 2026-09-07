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
    string? ClaimantName,
    string OurReference,
    string? YourReference,
    IReadOnlyList<string> ReportFor,
    DateOnly? ReportDate,
    IReadOnlyList<ReportImageEvidence> Photos,
    IReadOnlyList<AcceptedReportSource> Sources,
    RepairSpecificationVersion? CurrentEstimate = null,
    ReportSignatory? Signatory = null,
    ReportGuideSources? Guides = null,
    string? ValuationCommentary = null);

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
    public const string RepairCostRequirement = CaseReportReadiness.CurrentEstimateRequirement;
    public const string LabourRateRequirement = CaseReportReadiness.LabourRateRequirement;

    public static AssessmentReportDraftPreparation Prepare(
        CaseAssessmentProjection assessment,
        RepairSpecificationVersion? currentEstimate = null,
        ReportSignatory? signatory = null)
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

        Require(
            signatory?.IsComplete == true,
            CaseReportReadiness.SignatoryRequirement, "Case sign-off account",
            "The Case has no complete sign-off Engineer tuple.",
            "Select an eligible sign-off Engineer with a signature on file.");

        // The report's repair cost is the Current estimate's canonical total
        // (EXT-09, FRD-11 § Estimate VAT on the rendered report). There is no
        // hand-typed cost path.
        Require(
            currentEstimate is not null,
            RepairCostRequirement, "Estimates",
            "No estimate is marked Current on the case (EXT-09).",
            "Use an estimate on the Assessment page.");
        Require(
            currentEstimate is null || currentEstimate.Details.HourlyRate > 0m,
            LabourRateRequirement, "Estimates",
            "The Current estimate has no labour rate, and the report prints the hourly rate.",
            "Record the labour rate on the Current estimate.");

        return new(reasons);
    }

    public static AssessmentReportProjectionResult Project(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var assessment = input.Assessment;
        var preparation = Prepare(assessment, input.CurrentEstimate, input.Signatory);
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

        var claimantName = RequiredReviewValue(input.ClaimantName, "claimant name");
        var yourReference = RequiredReviewValue(input.YourReference, "claim number");
        var incidentDate = RequiredReviewDate(assessment.CaseOwned.IncidentDate, "incident date");
        var instructionDate = RequiredReviewDate(
            assessment.CaseOwned.InstructionDate,
            "instruction date");
        var assessmentMethod = MapAssessmentMethod(assessment.CaseOwned.InspectionMode)
            ?? throw new InvalidDataException(
                "A Review case is missing its accepted inspection method.");
        var signatory = input.Signatory!;

        var snapshot = new AssessmentReportSnapshot(
            OurReference: input.OurReference,
            YourReference: yourReference,
            ReportDate: reportDate,
            ClaimantName: claimantName,
            IncidentDate: incidentDate,
            InstructionsReceived: instructionDate,
            Assessed: ParseDate(Field(fields, AssessmentVocabulary.IncidentAssessed)) ?? default,
            ReportFor: input.ReportFor,
            Vehicle: BuildVehicle(assessment, fields),
            Outcome: MapOutcome(Field(fields, AssessmentVocabulary.Outcome)!),
            LegalStatus: Field(fields, AssessmentVocabulary.LegalStatus)!,
            UnroadworthyReason: Field(fields, AssessmentVocabulary.UnroadworthyReason),
            ImpactSeverity: Field(fields, AssessmentVocabulary.ImpactSeverity)!,
            ImpactLocation: Field(fields, AssessmentVocabulary.ImpactLocation)!,
            AssessmentMethod: assessmentMethod!,
            LocationAddress: assessment.CaseOwned.InspectionAddress,
            EngineerValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueEngineer)) ?? 0m,
            RetailValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueRetail)) ?? 0m,
            TradeValue: ParseMoney(Field(fields, AssessmentVocabulary.ValueTrade)) ?? 0m,
            SalvageCategory: Field(fields, AssessmentVocabulary.SalvageCategory),
            SalvageValue: ParseMoney(Field(fields, AssessmentVocabulary.SalvageValue)),
            Costs: costs,
            NewParts: LinesOfType(lines, "new_part"),
            Repairs: LinesOfType(lines, "repair"),
            Operations: LinesOfType(
                lines,
                "check_labour", "paint_new", "paint_repair", "paint_blend", "paint_prep",
                "specialist_fixed", "specialist_wu"),
            Damage: BuildDamage(fields),
            Settlement: BuildSettlement(fields, input.CurrentEstimate, costs),
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
            ReportDateOverridden: reportDateOverridden);

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

    private static string? Field(IReadOnlyDictionary<string, string?> fields, string path) =>
        fields.GetValueOrDefault(path);

    private static ReportVehicle BuildVehicle(
        CaseAssessmentProjection assessment,
        IReadOnlyDictionary<string, string?> fields)
    {
        var mileageSource = Field(fields, AssessmentVocabulary.VehicleMileageSource) ?? "tbc";
        var mileage = assessment.CaseOwned.Mileage;
        var mileageUnit = assessment.CaseOwned.MileageUnit ?? "miles";
        var mileageDescription = mileage is { } value
            ? $"{value:N0} {mileageUnit}"
            : "To be confirmed";

        return new ReportVehicle(
            Registration: assessment.CaseOwned.Registration ?? string.Empty,
            Make: assessment.CaseOwned.Make ?? string.Empty,
            Model: assessment.CaseOwned.Model ?? string.Empty,
            Year: Field(fields, AssessmentVocabulary.VehicleYear) ?? string.Empty,
            VehicleType: Field(fields, AssessmentVocabulary.VehicleType) ?? string.Empty,
            Condition: Field(fields, AssessmentVocabulary.VehicleCondition) ?? string.Empty,
            MileageDescription: mileageDescription,
            MileageSource: mileageSource,
            Vin: Field(fields, AssessmentVocabulary.VehicleVin),
            Engine: Field(fields, AssessmentVocabulary.VehicleEngineCc),
            Fuel: Field(fields, AssessmentVocabulary.VehicleFuel),
            VinChecked: ParseFlag(Field(fields, AssessmentVocabulary.VehicleVinChecked)),
            Transmission: AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.VehicleTransmission)),
            Colour: Field(fields, AssessmentVocabulary.VehicleColour),
            Body: Field(fields, AssessmentVocabulary.VehicleBody),
            TaxExpiry: ParseDate(Field(fields, AssessmentVocabulary.VehicleTaxExpiry)),
            MotExpiry: ParseDate(Field(fields, AssessmentVocabulary.VehicleMotExpiry)),
            AirbagsDeployed: Field(fields, AssessmentVocabulary.VehicleAirbagsDeployed),
            FaultCodes: Field(fields, AssessmentVocabulary.VehicleFaultCodes),
            TemporaryRepairsPossible: ParseFlag(Field(fields, AssessmentVocabulary.VehicleTemporaryRepairsPossible)),
            TemporaryRepairMethod: Field(fields, AssessmentVocabulary.VehicleTemporaryRepairMethod),
            TemporaryRepairCost: ParseMoney(Field(fields, AssessmentVocabulary.VehicleTemporaryRepairCost)));
    }

    private static ReportDamage BuildDamage(IReadOnlyDictionary<string, string?> fields)
    {
        var impacts = AssessmentPolicy.ParseImpacts(Field(fields, AssessmentVocabulary.DamageImpacts))
            .Select(impact => new ReportImpact(
                AssessmentReportPresentation.DamageZone(impact.Zone),
                AssessmentReportPresentation.DamageSeverity(impact.Severity),
                impact.Note))
            .ToArray();
        return new(
            impacts,
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageTyreRightFront)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageTyreLeftFront)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageTyreRightRear)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageTyreLeftRear)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageBeltRightFront)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageBeltLeftFront)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageBeltRightRear)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageBeltLeftRear)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageSpareTyre)),
            AssessmentReportPresentation.AssessmentCode(Field(fields, AssessmentVocabulary.DamageCentreBelt)),
            Field(fields, AssessmentVocabulary.DamageUnrelated),
            ParseMoney(Field(fields, AssessmentVocabulary.DamageUnrelatedDeduction)),
            Field(fields, AssessmentVocabulary.DamageMaterialTransfer));
    }

    private static ReportSettlement BuildSettlement(
        IReadOnlyDictionary<string, string?> fields,
        RepairSpecificationVersion? estimate,
        ReportRepairCosts costs)
    {
        var engineerValue = ParseMoney(Field(fields, AssessmentVocabulary.ValueEngineer)) ?? 0m;
        var betterment = ParseMoney(Field(fields, AssessmentVocabulary.SettlementBetterment));
        var salvage = ParseMoney(Field(fields, AssessmentVocabulary.SalvageValue));
        return new(
            ParseMoney(Field(fields, AssessmentVocabulary.SettlementExcess)),
            betterment,
            ParseFlag(Field(fields, AssessmentVocabulary.SettlementClaimantVatRegistered)),
            ParseMoney(Field(fields, AssessmentVocabulary.SettlementReserve)),
            engineerValue - (costs.Total - (betterment ?? 0m)) - (salvage ?? 0m),
            estimate?.Details.RepairDays,
            Field(fields, AssessmentVocabulary.SettlementRepairDelays),
            Field(fields, AssessmentVocabulary.SettlementReportDelay),
            ParseMoney(Field(fields, AssessmentVocabulary.SettlementStoragePerDay)),
            ParseMoney(Field(fields, AssessmentVocabulary.CostRecoveryCharge)),
            ParseDate(Field(fields, AssessmentVocabulary.SettlementHireStart)),
            ParseMoney(Field(fields, AssessmentVocabulary.SettlementHireDailyCost)),
            ParseMoney(Field(fields, AssessmentVocabulary.SettlementDiminution)),
            Field(fields, AssessmentVocabulary.SettlementSalvageAt),
            Field(fields, AssessmentVocabulary.SettlementSalvageAgent),
            Field(fields, AssessmentVocabulary.SettlementSalvageAgentReference),
            ParseFlag(Field(fields, AssessmentVocabulary.SettlementSalvageMoved)),
            ParseFlag(Field(fields, AssessmentVocabulary.SettlementSalvageOwnerRetains)),
            ParseFlag(Field(fields, AssessmentVocabulary.SettlementSalvageValueAgreed)),
            ParseDate(Field(fields, AssessmentVocabulary.SettlementSalvageSettled)));
    }

    /// <summary>
    /// Groups the Current estimate's line descriptions for the report's
    /// parts/repairs/operations lists. Every estimate line is already
    /// confirmed by the time this runs — <see cref="AssessmentPolicy.EvaluatePostReviewReadiness"/>
    /// blocks the whole draft on the first unconfirmed line, of any type —
    /// so this only has to group by type and drop blank descriptions.
    /// </summary>
    private static string[] LinesOfType(
        IReadOnlyList<CaseEstimateLineRecord> lines, params ReadOnlySpan<string> types)
    {
        var typeSet = new HashSet<string>(types.ToArray(), StringComparer.Ordinal);
        return lines
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

    /// <summary>
    /// ENG-037: persisted dates are invariant <c>yyyy-MM-dd</c>. Parsing them
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
    RenderedReportArtifact? Draft,
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
        // H3: the report journey (preview included) never depends on an EVA
        // export cycle — the workspace opening rule's state set without its
        // export clause.
        if (access is null || !AssessmentAccessPolicy.CanOpenReports(access))
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var input = await source.GetAsync(caseId, actor, cancellationToken);
        if (input is null)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotFound, null, []);
        }

        var projected = AssessmentReportProjection.Project(input with
        {
            ReportDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
        });
        if (!projected.IsReady)
        {
            return new(GenerateCaseAssessmentReportDraftOutcome.NotReady, null, projected.Reasons);
        }

        var draft = await generate.ExecuteAsync(projected.Snapshot!, kind, cancellationToken);
        return new(GenerateCaseAssessmentReportDraftOutcome.Generated, draft, []);
    }
}
