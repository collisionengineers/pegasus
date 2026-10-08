using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record AssessmentFieldToolItem(
    string Path,
    string Value,
    string RecordedByKind,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc);

internal sealed record EstimateLineToolItem(
    Guid LineId,
    int Position,
    string Type,
    string? GuideCode,
    string? Description,
    decimal? WorkUnits,
    decimal? Price,
    bool Unpriced,
    string? PartNumber,
    string? Betterment,
    string? EvidenceLabel,
    string? Justification,
    string RecordedByKind,
    decimal? PaintWorkUnits = null,
    int? Quantity = null,
    decimal? Materials = null,
    string? AmendedBy = null);

internal sealed record EstimateLineToolInput(
    [property: Description("Line type: rnr (remove and refit), repair, new_part, check_labour, paint_new (paint a new part), paint_repair (paint a repaired panel), paint_blend, paint_prep (paint preparation), specialist_fixed (specialist work at a fixed price) or specialist_wu (specialist work priced by work units at the labour rate). pegasus_vocabulary_get lists them with their meanings.")] string Type,
    [property: Description("The guide (Glass's/Audatex) operation code, if any.")] string? GuideCode = null,
    [property: Description("What the line is, as the report prints it.")] string? Description = null,
    [property: Description("Panel labour hours.")] decimal? WorkUnits = null,
    [property: Description("Price per unit in pounds, excluding VAT; multiplied by quantity.")] decimal? Price = null,
    [property: Description("True marks the line To be confirmed: its price is not known yet. A line that carries a price is not To be confirmed, so a price clears this flag.")] bool Unpriced = false,
    [property: Description("Manufacturer part number.")] string? PartNumber = null,
    [property: Description("Betterment applied to the line, as text.")] string? Betterment = null,
    [property: Description("The kind of source the line's figure stands on, one of: official (manufacturer or official repair/price data), reference (a published reference or guide: Glass's/Audatex times, ABP), case (evidence on this Case: photos, documents, repairer estimate), judgement (the assessor's professional judgement). Write the source itself in justification.")] string? EvidenceLabel = null,
    [property: Description("Why the line is needed and where its figures come from (at most 500 characters).")] string? Justification = null,
    [property: Description("Paint labour hours.")] decimal? PaintWorkUnits = null,
    [property: Description("Quantity; defaults to 1.")] int? Quantity = null,
    [property: Description("Paint and materials on this line, in pounds.")] decimal? Materials = null,
    [property: Description("The existing line this row is (lineId from pegasus_estimate_get), so the line keeps its source evidence; omit for a new line.")] Guid? LineId = null);

internal sealed record EstimateDiscountsToolInput(
    [property: Description("Parts discount as a fraction of one, 0 to 1 (0.1 is 10%).")] decimal Parts = 0m,
    [property: Description("Materials discount as a fraction of one, 0 to 1.")] decimal Materials = 0m,
    [property: Description("Specialist discount as a fraction of one, 0 to 1.")] decimal Specialist = 0m,
    [property: Description("Overall discount as a fraction of one, 0 to 1.")] decimal Overall = 0m);

internal sealed record EstimateTotalsToolItem(
    decimal Parts,
    decimal PanelLabour,
    decimal PaintLabour,
    decimal Materials,
    decimal Specialist,
    decimal Net,
    decimal VatPercent,
    decimal Vat,
    decimal Gross);

internal sealed record EstimateToolItem(
    Guid EstimateId,
    int Version,
    string Name,
    string State,
    string SourceRoute,
    bool IsCurrent,
    Guid? AiJobId,
    decimal? LabourRate,
    bool RegionalUplift,
    decimal? OtherCosts,
    decimal VatPercent,
    string RepairerVatStatus,
    IReadOnlyList<string> VatCategories,
    bool VatTreatmentPending,
    EstimateDiscountsToolInput Discounts,
    IReadOnlyList<EstimateLineToolItem> Lines,
    EstimateTotalsToolItem Totals,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc);

internal sealed record EstimateSaveToolResult(
    Guid CaseId,
    long CaseVersion,
    EstimateToolItem Estimate,
    string OperationKey,
    string CorrelationId);

internal sealed record EstimateSnapshotToolItem(
    Guid SnapshotId,
    int Number,
    string Kind,
    DateTimeOffset CreatedAtUtc,
    decimal Gross);

internal sealed record EstimateGetToolResult(
    Guid CaseId,
    EstimateToolItem Estimate,
    IReadOnlyList<EstimateSnapshotToolItem> Snapshots,
    string CorrelationId);

internal sealed record EstimateActToolResult(
    Guid CaseId,
    long CaseVersion,
    EstimateToolItem Estimate,
    string OperationKey,
    string CorrelationId);

internal sealed record EstimateImportToolResult(
    Guid CaseId,
    Guid EstimateId,
    string Name,
    string OperationKey,
    string CorrelationId);
internal sealed record EstimateListToolItem(
    Guid EstimateId,
    int Version,
    string State,
    string Source,
    string Name,
    bool IsCurrent);

internal sealed record EstimateListToolResult(
    Guid CaseId,
    IReadOnlyList<EstimateListToolItem> Estimates,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record AssessmentCaseOwnedToolData(
    string? Registration,
    string? Make,
    string? Model,
    string? Year,
    long? Mileage,
    string? MileageUnit,
    string MileageSource,
    string? IncidentDate,
    // The Case's received date, which the report prints as the date
    // instructions were received; every Case has one.
    string ReceivedDate,
    string? InspectionDate,
    string? InspectionMode,
    string? InspectionAddress);

internal sealed record AssessmentReadinessToolItem(
    string Requirement,
    string Source,
    string WhyOutstanding,
    string HowToResolve);

internal sealed record AssessmentGetToolResult(
    Guid CaseId,
    string Reference,
    long CaseVersion,
    string State,
    Guid? AssignedEngineerId,
    IReadOnlyList<AssessmentFieldToolItem> Fields,
    IReadOnlyList<EstimateLineToolItem> EstimateLines,
    AssessmentCaseOwnedToolData CaseOwned,
    IReadOnlyList<AssessmentReadinessToolItem> Readiness,
    string CorrelationId);

internal sealed record AssessmentUpdateToolResult(
    Guid CaseId,
    long CaseVersion,
    string State,
    IReadOnlyList<AssessmentFieldToolItem> Fields,
    IReadOnlyList<EstimateLineToolItem> EstimateLines,
    IReadOnlyList<AssessmentReadinessToolItem> Readiness,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor assessment and estimate tools (FRD-10, ADR-0064): direct
/// writes over the same Core commands, edit lease, and version guards as a
/// staff save, attributed to the Automation actor and limited to fields a
/// staff member records on the Case, professional findings included
/// (operator, 7 October 2026). A recorded value is the Case's value whoever
/// recorded it, shown with its source tag; there is no per-field review
/// (operator, 25 September 2026). Only staff put an estimate in use.
/// </summary>
[McpServerToolType]
internal sealed class AssessmentMcpTools(
    IGetCaseAssessment getAssessment,
    ISaveAssessment saveAssessment,
    ISaveEstimate saveEstimate,
    IListCaseEstimates caseEstimates,
    IListCaseEstimatesByCursor listEstimates,
    IDuplicateEstimate duplicateEstimate,
    IDiscardEstimate discardEstimate,
    IScaleRepairSpecification scaleEstimate,
    IRemoveRepairSpecificationScaling removeEstimateScaling,
    IRestoreRepairSpecificationSnapshot restoreEstimateSnapshot,
    IRepairSpecificationSnapshotStore estimateSnapshots,
    ICaseWorkflowQueries workflowQueries,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases,
    IImportRawEstimate? importRawEstimate = null)
{
    [McpServerTool(
        Name = "pegasus_estimate_import",
        Title = "Import retained estimate",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Imports one already-retained exact estimate document through Pegasus's canonical named raw-estimate import. The same Case version, parser route and replay rules as the Case UI apply; present an edit lease token for multi-step work or omit it and the tool holds the lease for this one command. An automation import lands as a Draft; a staff member puts it in use with Use repair spec.")]
    public async Task<EstimateImportToolResult> ImportEstimateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The estimate's name on its tab (at most 100 characters).")] string name,
        [Description("The retained document's occurrence identifier (from pegasus_case_get).")] Guid occurrenceId,
        [Description("The retained document's exact version identifier.")] Guid documentVersionId,
        [Description("The retained document's SHA-256, as pegasus_case_get and the download report it.")] string sha256,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.AssessmentScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_estimate_import",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                AutomationMcpErrors.RequireId(occurrenceId, "document occurrence identifier");
                AutomationMcpErrors.RequireId(documentVersionId, "document version identifier");
                var importer = importRawEstimate
                    ?? throw new McpException("Estimate import is unavailable in this runtime.");
                var imported = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    token => importer.ExecuteAsync(
                        new(context.Actor, caseId, expectedVersion, token,
                            occurrenceId, documentVersionId, sha256, key, name),
                        cancellationToken),
                    cancellationToken);
                return new EstimateImportToolResult(
                    caseId, imported.EstimateId, name.Trim(), key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_estimate_save",
        Title = "Save estimate",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Saves an estimate (repair spec) on a case, as the Case's Repair Spec editor does: creates a named estimate, which lands as an AI draft in Draft, or, with estimateId, edits any live estimate in place (the Current one included; its report goes stale). On an edit, send each kept line's lineId (pegasus_estimate_get) so it keeps its source evidence; header values you omit keep their recorded values. Only a staff member puts an estimate in use (Use repair spec). Citing the Estimate AI job the work fulfils (aiJobId) is optional. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command). Rates are per hour in pounds. VAT follows the repairer's VAT status: Registered charges VAT on everything, NotRegistered on parts and materials, Unknown (the default) on nothing until the status is known; vatCategories overrides which categories carry VAT. Line types and evidence labels are listed by pegasus_vocabulary_get.")]
    public async Task<EstimateSaveToolResult> SaveEstimateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why the estimate is being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("Estimate name shown on its tab (at most 100 characters).")] string name,
        [Description("The ordered estimate lines; the whole collection is replaced.")] IReadOnlyList<EstimateLineToolInput> lines,
        [Description("Existing estimate to edit; omit to create a new one.")] Guid? estimateId = null,
        [Description("The Estimate AI job this estimate fulfils, if any.")] Guid? aiJobId = null,
        [Description("One hourly rate for both panel and paint labour.")] decimal? labourRate = null,
        [Description("Other costs amount.")] decimal? otherCosts = null,
        [Description("VAT percentage, 0 to 100; defaults to 20.")] decimal? vatPercent = null,
        [Description("The repairer's VAT status: Registered, NotRegistered or Unknown.")] string? repairerVatStatus = null,
        [Description("Override of which categories carry VAT: any of Labour, Parts, Materials, Specialist; an empty list charges none.")] IReadOnlyList<string>? vatCategories = null,
        [Description("Discounts by category, each a fraction of one (0.1 is 10%).")] EstimateDiscountsToolInput? discounts = null,
        [Description("Whether the regional labour uplift applies.")] bool? regionalUplift = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(
            AutomationMcp.AssessmentScope,
            cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_estimate_save",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                ArgumentNullException.ThrowIfNull(lines);
                var status = ParseVatStatus(repairerVatStatus);
                var categories = ParseVatCategories(vatCategories);

                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    normalizedKey,
                    async token =>
                    {
                        var existing = estimateId is { } id
                            ? await FindEstimateAsync(caseId, id, cancellationToken)
                            : null;
                        var recorded = existing?.Details;
                        var recordedVat = recorded?.VatPolicy ?? EstimateVatPolicy.For(RepairerVatStatus.Unknown);
                        return await saveEstimate.ExecuteAsync(
                            new(
                                caseId,
                                expectedVersion,
                                context.Actor,
                                normalizedKey,
                                reason,
                                token,
                                estimateId,
                                new(
                                    name,
                                    labourRate ?? recorded?.LabourRate,
                                    otherCosts ?? recorded?.OtherCosts,
                                    vatPercent ?? recorded?.VatPercent ?? EstimatePolicy.DefaultVatPercent,
                                    discounts is { } d
                                        ? new(d.Parts, d.Materials, d.Specialist, d.Overall)
                                        : recorded?.Discounts,
                                    EstimateVatPolicy.Revised(
                                        recordedVat,
                                        status ?? recordedVat.RepairerStatus,
                                        categories ?? recordedVat.Categories),
                                    recorded?.Rate,
                                    regionalUplift ?? recorded?.RegionalUplift ?? false),
                                lines.Select(MapLineInput).ToArray(),
                                new(RepairSpecificationSourceRoute.AiDraft, null, null, null),
                                aiJobId,
                                existing is null && lines.All(line => line.LineId is null)
                                    ? null
                                    : lines.Select(line => line.LineId).ToArray())
                            {
                                Supplementary = existing?.Supplementary,
                            },
                            cancellationToken);
                    },
                    cancellationToken);
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new EstimateSaveToolResult(
                    caseId,
                    workflow.Version,
                    MapEstimate(saved),
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_estimate_get",
        Title = "Get case estimate",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns one estimate in full: its header (rate, VAT treatment, discounts, uplift), every line with its lineId and evidence, its totals, and the snapshots it can be restored to.")]
    public async Task<EstimateGetToolResult> GetEstimateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The estimate identifier (pegasus_estimate_list).")] Guid estimateId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.AssessmentScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_estimate_get",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                AutomationMcpErrors.RequireId(estimateId, "estimate identifier");
                var estimate = await FindEstimateAsync(caseId, estimateId, cancellationToken);
                var snapshots = await estimateSnapshots.ListAsync(caseId, estimateId, cancellationToken);
                return new EstimateGetToolResult(
                    caseId,
                    MapEstimate(estimate),
                    snapshots
                        .Select(snapshot => new EstimateSnapshotToolItem(
                            snapshot.Id, snapshot.Number, snapshot.Kind.ToString(),
                            snapshot.CreatedAtUtc, snapshot.Gross))
                        .ToArray(),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_estimate_act",
        Title = "Act on case estimate",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Performs one of the Repair Spec acts a member of staff performs on an estimate: duplicate (a new copy; needs a reason), discard (needs a reason; the estimate in use cannot be discarded), scale (scale the estimate to targetPercentOfValue of the Engineer's Value, with optional labour-rate and price floors), remove_scaling, or restore (back to a snapshot from pegasus_estimate_get). Putting an estimate in use stays a staff act. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<EstimateActToolResult> ActOnEstimateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The estimate acted on.")] Guid estimateId,
        [Description("duplicate, discard, scale, remove_scaling or restore.")] string action,
        [Description("Why (case history reason); required to duplicate or discard.")] string? reason = null,
        [Description("scale: the target as a percentage of the Engineer's Value.")] decimal? targetPercentOfValue = null,
        [Description("scale: the lowest labour rate per hour scaling may reach; defaults to 50.")] decimal? floorLabourRate = null,
        [Description("scale: the lowest percentage of a price scaling may reach; defaults to 65.")] decimal? floorPricePercent = null,
        [Description("restore: the snapshot to restore.")] Guid? snapshotId = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.AssessmentScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_estimate_act",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                AutomationMcpErrors.RequireId(estimateId, "estimate identifier");
                var actor = context.Actor;
                string Reason() => string.IsNullOrWhiteSpace(reason)
                    ? throw new McpException("Duplicating or discarding an estimate needs a reason.")
                    : reason;
                var result = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    actor,
                    key,
                    token => action?.Trim() switch
                    {
                        "duplicate" => duplicateEstimate.ExecuteAsync(
                            new(caseId, expectedVersion, actor, key, Reason(), token, estimateId),
                            cancellationToken),
                        "discard" => discardEstimate.ExecuteAsync(
                            new(caseId, expectedVersion, actor, key, Reason(), token, estimateId),
                            cancellationToken),
                        "scale" => scaleEstimate.ExecuteAsync(
                            new(caseId, expectedVersion, actor, key, token, estimateId,
                                targetPercentOfValue
                                    ?? throw new McpException("Scaling needs targetPercentOfValue."),
                                new(floorLabourRate ?? ScalingFloors.Default.LabourRatePerHour,
                                    floorPricePercent ?? ScalingFloors.Default.PricePercent)),
                            cancellationToken),
                        "remove_scaling" => removeEstimateScaling.ExecuteAsync(
                            new(caseId, expectedVersion, actor, key, token, estimateId),
                            cancellationToken),
                        "restore" => restoreEstimateSnapshot.ExecuteAsync(
                            new(caseId, expectedVersion, actor, key, token, estimateId,
                                snapshotId ?? throw new McpException("Restoring needs snapshotId.")),
                            cancellationToken),
                        _ => throw new McpException(
                            "The action must be duplicate, discard, scale, remove_scaling or restore."),
                    },
                    cancellationToken);
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new EstimateActToolResult(
                    caseId,
                    workflow.Version,
                    MapEstimate(result),
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_estimate_list",
        Title = "List case estimates",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists a bounded page of estimate headers on a case in version order with state, source route and Current status.")]
    public async Task<EstimateListToolResult> ListEstimatesAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("Opaque cursor returned by the previous page; omit for the first page.")] string? cursor = null,
        [Description("Page size between 1 and 100; omit for 50.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(
            AutomationMcp.AssessmentScope,
            cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_estimate_list",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (await workflowQueries.GetAsync(caseId, cancellationToken) is null)
                {
                    throw new McpException("The case was not found.");
                }
                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var estimates = await listEstimates.ExecuteAsync(
                    new(context.Actor, caseId, cursor, effectiveLimit), cancellationToken);
                return new EstimateListToolResult(
                    caseId,
                    estimates.Items.Select(MapEstimateList).ToArray(),
                    estimates.NextCursor,
                    effectiveLimit,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_assessment_get",
        Title = "Get case assessment",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the recorded assessment surface for one case: every recorded field value with its provenance, the ordered estimate lines, the case-owned fields the assessment reads (registration, make, model, mileage, incident, received and inspection dates, inspection mode and address; receivedDate is the Case's received date, which the report prints as the date instructions were received), and the readiness list naming what is still outstanding.")]
    public async Task<AssessmentGetToolResult> GetAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(
            AutomationMcp.AssessmentScope,
            cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_assessment_get",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var projection = await getAssessment.ExecuteAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new AssessmentGetToolResult(
                    projection.CaseId,
                    projection.Reference,
                    projection.CaseVersion,
                    projection.State.ToString(),
                    projection.AssignedEngineerId,
                    projection.Fields.Select(MapField).ToArray(),
                    projection.EstimateLines.Select(MapLine).ToArray(),
                    MapCaseOwned(projection.CaseOwned),
                    projection.Readiness.Select(MapReadiness).ToArray(),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_assessment_update",
        Title = "Update case assessment",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records assessment fields that staff record on the Case, professional findings included (for example assessment.outcome, assessment.legal_status for roadworthiness, assessment.salvage_category, assessment.values.engineer), under the expected case version; present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command. pegasus_vocabulary_get lists every path with its accepted codes. A value written by automation is the Case's value, attributed to the Automation actor and shown with its source tag. Case-owned facts (use pegasus_case_update_details), fields derived from damage.impacts, fields the DVLA/DVSA vehicle lookup fills and fields with no staff editor on the Case are refused, naming the field.")]
    public async Task<AssessmentUpdateToolResult> UpdateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why these values are being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("Assessment values keyed by field path, each as a string, limited to fields staff can record on the Case; a null value clears the field. An enumerated field takes one of its codes, and damage.impacts takes a JSON array in the format pegasus_vocabulary_get gives for it.")] Dictionary<string, string?>? fields = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(
            AutomationMcp.AssessmentScope,
            cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_assessment_update",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                foreach (var path in fields?.Keys ?? Enumerable.Empty<string>())
                {
                    RequireGenericWrite(path);
                }

                var projection = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    normalizedKey,
                    token => saveAssessment.ExecuteAsync(
                        new(
                            caseId,
                            expectedVersion,
                            context.Actor,
                            normalizedKey,
                            reason,
                            token,
                            fields ?? new Dictionary<string, string?>(StringComparer.Ordinal)),
                        cancellationToken),
                    cancellationToken);
                return new AssessmentUpdateToolResult(
                    projection.CaseId,
                    projection.CaseVersion,
                    projection.State.ToString(),
                    projection.Fields.Select(MapField).ToArray(),
                    projection.EstimateLines.Select(MapLine).ToArray(),
                    projection.Readiness.Select(MapReadiness).ToArray(),
                    normalizedKey,
                    normalizedKey);
            }),
            cancellationToken);
    }

    /// <summary>
    /// Refuses a generic automation write of a path with no staff editor on
    /// the Case (FRD-10), the one rule only the Web can answer because it owns
    /// the editor list. An unknown, case-owned, impact-derived or
    /// lookup-derived path falls through to Core's NormalizeWritableField,
    /// which names each.
    /// </summary>
    internal static void RequireGenericWrite(string path)
    {
        if (!AssessmentVocabulary.Definitions.ContainsKey(path)
            || AssessmentVocabulary.DerivedPaths.Contains(path)
            || AssessmentVocabulary.LookupDerivedPaths.Contains(path))
        {
            return;
        }
        if (!CaseWorkspaceLabels.Editors.HasStaffEditor(path))
        {
            throw new McpException(
                $"The field '{path}' has no staff editor on the Case, so automation cannot write it: "
                + "staff could neither change nor clear the value.");
        }
    }

    /// <summary>
    /// Why pegasus_assessment_update refuses a vocabulary path, or null when
    /// it writes it: the vocabulary read names the reason beside the path.
    /// </summary>
    internal static string? WriteRefusal(string path) =>
        AssessmentVocabulary.DerivedPaths.Contains(path)
            ? "Derived from damage.impacts."
            : AssessmentVocabulary.LookupDerivedPaths.Contains(path)
                ? "Filled by the DVLA/DVSA vehicle lookup."
                : !CaseWorkspaceLabels.Editors.HasStaffEditor(path)
                    ? "No staff editor on the Case."
                    : null;

    private async Task<RepairSpecificationVersion> FindEstimateAsync(
        Guid caseId, Guid estimateId, CancellationToken cancellationToken) =>
        (await caseEstimates.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken))
            .SingleOrDefault(estimate => estimate.SpecificationId == estimateId)
            ?? throw new McpException("The estimate was not found on this case.");

    private static RepairerVatStatus? ParseVatStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<RepairerVatStatus>(value.Trim(), ignoreCase: true, out var status)
                && Enum.IsDefined(status)
                ? status
                : throw new McpException("The repairer VAT status must be Registered, NotRegistered or Unknown.");

    private static EstimateVatCategories? ParseVatCategories(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return null;
        }
        var categories = EstimateVatCategories.None;
        foreach (var value in values)
        {
            categories |= value?.Trim() switch
            {
                nameof(EstimateVatCategories.Labour) => EstimateVatCategories.Labour,
                nameof(EstimateVatCategories.Parts) => EstimateVatCategories.Parts,
                nameof(EstimateVatCategories.Materials) => EstimateVatCategories.Materials,
                nameof(EstimateVatCategories.Specialist) => EstimateVatCategories.Specialist,
                _ => throw new McpException("A VAT category must be Labour, Parts, Materials or Specialist."),
            };
        }
        return categories;
    }

    private static string[] VatCategoryNames(EstimateVatCategories categories) =>
        new[]
        {
            EstimateVatCategories.Labour, EstimateVatCategories.Parts,
            EstimateVatCategories.Materials, EstimateVatCategories.Specialist,
        }
        .Where(category => (categories & category) == category)
        .Select(category => category.ToString())
        .ToArray();

    private static AssessmentFieldToolItem MapField(AssessmentFieldValue field) => new(
        field.Path,
        field.Value,
        field.RecordedByKind.ToString(),
        field.RecordedBy,
        field.RecordedAtUtc);

    private static EstimateLineToolItem MapLine(CaseEstimateLineRecord line) => new(
        line.Id,
        line.Position,
        line.Type,
        line.GuideCode,
        line.Description,
        line.WorkUnits,
        line.Price,
        line.Unpriced,
        line.PartNumber,
        line.Betterment,
        line.EvidenceLabel,
        line.Justification,
        line.RecordedByKind.ToString(),
        line.PaintWorkUnits,
        line.Quantity,
        line.Materials,
        line.AmendedBy);

    private static EstimateLineInput MapLineInput(EstimateLineToolInput line) => new(
        line.Type,
        line.GuideCode,
        line.Description,
        line.WorkUnits,
        line.Price,
        line.Unpriced,
        line.PartNumber,
        line.Betterment,
        line.EvidenceLabel,
        line.Justification,
        line.PaintWorkUnits,
        line.Quantity,
        line.Materials);

    private static EstimateToolItem MapEstimate(RepairSpecificationVersion estimate)
    {
        var totals = EstimateTotals.Compute(estimate);
        var details = estimate.Details;
        return new(
            estimate.SpecificationId,
            estimate.Version,
            details.Name,
            estimate.State.ToString(),
            estimate.Source.Route.ToString(),
            estimate.IsCurrent,
            estimate.AiJobId,
            details.HourlyRate,
            details.RegionalUplift,
            details.OtherCosts,
            details.VatPercent,
            details.VatPolicy.RepairerStatus.ToString(),
            VatCategoryNames(details.VatPolicy.Categories),
            details.VatPolicy.TreatmentPending,
            new(details.AppliedDiscounts.Parts, details.AppliedDiscounts.Materials,
                details.AppliedDiscounts.Specialist, details.AppliedDiscounts.Overall),
            estimate.Lines.Select(MapLine).ToArray(),
            new(
                totals.Printed.Parts,
                totals.Printed.PanelLabour,
                totals.Printed.PaintLabour,
                totals.Printed.Materials,
                totals.Printed.Specialist,
                totals.Printed.Net,
                totals.VatPercent,
                totals.Printed.Vat,
                totals.Printed.Gross),
            estimate.CreatedBy,
            estimate.CreatedAtUtc);
    }

    private static EstimateListToolItem MapEstimateList(CaseEstimatePageItem estimate) => new(
        estimate.SpecificationId,
        estimate.Version,
        estimate.State.ToString(),
        estimate.Source.Route.ToString(),
        estimate.Name,
        estimate.IsCurrent);

    private static AssessmentCaseOwnedToolData MapCaseOwned(AssessmentCaseOwnedData data) => new(
        data.Registration,
        data.Make,
        data.Model,
        data.Year,
        data.Mileage,
        data.MileageUnit,
        data.MileageSource,
        data.IncidentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        data.ReceivedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        data.InspectionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        data.InspectionMode,
        data.InspectionAddress);

    private static AssessmentReadinessToolItem MapReadiness(AssessmentReadinessItem item) => new(
        item.Requirement,
        item.Source,
        item.WhyOutstanding,
        item.HowToResolve);

}
