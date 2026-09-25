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
    DateTimeOffset RecordedAtUtc,
    bool IsConfirmed,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAtUtc);

internal sealed record EstimateLineToolItem(
    int Position,
    string Type,
    string? GuideCode,
    string? Description,
    decimal? WorkUnits,
    decimal? Price,
    bool Unpriced,
    string? PartNumber,
    string? Betterment,
    string? Status,
    string? EvidenceLabel,
    string? Justification,
    string RecordedByKind,
    bool IsConfirmed,
    decimal? PaintWorkUnits = null,
    int? Quantity = null);

internal sealed record EstimateLineToolInput(
    string Type,
    string? GuideCode = null,
    string? Description = null,
    decimal? WorkUnits = null,
    decimal? Price = null,
    bool Unpriced = false,
    string? PartNumber = null,
    string? Betterment = null,
    string? Status = null,
    string? EvidenceLabel = null,
    string? Justification = null,
    decimal? PaintWorkUnits = null,
    int? Quantity = null,
    decimal? Materials = null);

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
    bool IsCurrent,
    string? CalculationBasis);

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

internal sealed record CaseUpdateDetailsToolResult(
    Guid CaseId,
    long CaseVersion,
    string State,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor assessment tools (the tranche specified by
/// ADR-0031 / FRD-10 (docs/adr/0031-automation-actor-contract-without-eva-export-tools.md,
/// docs/frd/frd-10-mcp-automation-and-actor-boundary.md)): direct writes over the same
/// Core commands, edit lease, and version guards as a staff save, attributed
/// to the Automation actor with the values stored unconfirmed until staff
/// review and limited to fields a staff member can confirm on the Case.
/// Structurally absent, on purpose: any finding-confirmation tool,
/// any report-approval tool, and any tool that dispatches anything outward.
/// </summary>
[McpServerToolType]
internal sealed class AssessmentMcpTools(
    IGetCaseAssessment getAssessment,
    ISaveAssessment saveAssessment,
    ICaseDataQueries caseDataQueries,
    ISaveCase saveCase,
    ISaveEstimate saveEstimate,
    IListCaseEstimatesByCursor listEstimates,
    ICaseWorkflowQueries workflowQueries,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
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
    [Description("Imports one already-retained exact estimate document through Pegasus's canonical named raw-estimate import. The same Case version, edit lease, parser route and replay rules as the Case UI apply.")]
    public async Task<EstimateImportToolResult> ImportEstimateAsync(
        Guid caseId,
        long expectedVersion,
        string editLeaseToken,
        string operationKey,
        string name,
        Guid occurrenceId,
        Guid documentVersionId,
        string sha256,
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
                var imported = await importer.ExecuteAsync(
                    new(context.Actor, caseId, expectedVersion, editLeaseToken,
                        occurrenceId, documentVersionId, sha256, key, name),
                    cancellationToken);
                return new EstimateImportToolResult(
                    caseId, imported.EstimateId, name.Trim(), key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_estimate_save",
        Title = "Save AI-draft estimate",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Saves an AI-draft estimate on a case (FRD-10 § AI job and estimate tools): creates a named Draft, or replaces the header and lines of an existing AI-draft estimate when estimateId is supplied. Requires the edit lease and expected case version like every case mutation, and must cite the Estimate job this client currently holds (aiJobId); the estimate always lands as Draft with unconfirmed lines and never becomes Current here — an Engineer does that with Use estimate. Rates are per hour in pounds; vatPercent is free per estimate and defaults to 20. Line types follow the estimate-line vocabulary (new_part, repair, rnr, paint_*, check_labour, specialist_*); workUnits are labour hours, paintWorkUnits paint hours, price is per unit and multiplied by quantity (default 1).")]
    public async Task<EstimateSaveToolResult> SaveEstimateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why the estimate is being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("The Estimate AI job this draft fulfils; must be taken by this client.")] Guid aiJobId,
        [Description("Estimate name shown on its tab (at most 100 characters).")] string name,
        [Description("The ordered estimate lines; the whole collection is replaced.")] IReadOnlyList<EstimateLineToolInput> lines,
        [Description("Existing AI-draft estimate to replace; omit to create a new one.")] Guid? estimateId = null,
        [Description("One hourly rate for both panel and paint labour.")] decimal? labourRate = null,
        [Description("Other costs amount.")] decimal? otherCosts = null,
        [Description("VAT percentage, 0 to 100; defaults to 20.")] decimal? vatPercent = null,
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
            aiJobId == Guid.Empty ? normalizedKey : aiJobId.ToString("D"),
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                AutomationMcpErrors.RequireId(aiJobId, "AI job identifier");
                if (string.IsNullOrWhiteSpace(editLeaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }
                ArgumentNullException.ThrowIfNull(lines);

                var saved = await saveEstimate.ExecuteAsync(
                    new(
                        caseId,
                        expectedVersion,
                        context.Actor,
                        normalizedKey,
                        reason,
                        editLeaseToken,
                        estimateId,
                        new(
                            name,
                            labourRate,
                            otherCosts,
                            vatPercent ?? EstimatePolicy.DefaultVatPercent),
                        lines.Select(MapLineInput).ToArray(),
                        new(RepairSpecificationSourceRoute.AiDraft, null, null, null),
                        aiJobId),
                    cancellationToken);
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new EstimateSaveToolResult(
                    caseId,
                    workflow.Version,
                    MapEstimate(saved),
                    normalizedKey,
                    aiJobId.ToString("D"));
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
    [Description("Returns the recorded assessment surface for one case: every recorded field value with provenance and its confirmed/unconfirmed mark, the ordered estimate lines, the case-owned fields the assessment reads (registration, make, model, mileage, incident, received and inspection dates, inspection mode and address; receivedDate is the Case's received date, which the report prints as the date instructions were received), and the readiness list naming what is still outstanding.")]
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
    [Description("Records assessment fields that staff can also record on the Case, under the case edit lease and expected version. Values written by automation stay unconfirmed until a staff member saves the field's Case section, which confirms or clears them. Professional findings, case-owned facts (use pegasus_case_update_details), fields derived from damage.impacts and fields with no staff editor on the Case are refused, naming the field. The optional workRequestId correlates the write with a Send to AI hand-off.")]
    public async Task<AssessmentUpdateToolResult> UpdateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why these values are being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("Scalar assessment values keyed by field path, limited to fields staff can record on the Case; a null value clears the field.")] Dictionary<string, string?>? fields = null,
        [Description("Unsupported on this generic command; use a named estimate command.")] IReadOnlyList<EstimateLineToolInput>? estimateLines = null,
        [Description("Optional Send to AI work-request identifier for round-trip correlation.")] string? workRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(
            AutomationMcp.AssessmentScope,
            cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        var binding = ParseWorkRequestId(workRequestId);
        return await auditor.RecordAsync(
            context,
            "pegasus_assessment_update",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            binding?.ToString("D") ?? normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (string.IsNullOrWhiteSpace(editLeaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }
                if (estimateLines is not null)
                {
                    throw new McpException(
                        "Estimate lines must be changed through a named estimate command.");
                }
                foreach (var path in fields?.Keys ?? Enumerable.Empty<string>())
                {
                    RequireGenericWrite(path);
                }

                var projection = await saveAssessment.ExecuteAsync(
                    new(
                        caseId,
                        expectedVersion,
                        context.Actor,
                        normalizedKey,
                        reason,
                        editLeaseToken,
                        fields ?? new Dictionary<string, string?>(StringComparer.Ordinal),
                        estimateLines?.Select(MapLineInput).ToArray(),
                        binding),
                    cancellationToken);
                return new AssessmentUpdateToolResult(
                    projection.CaseId,
                    projection.CaseVersion,
                    projection.State.ToString(),
                    projection.Fields.Select(MapField).ToArray(),
                    projection.EstimateLines.Select(MapLine).ToArray(),
                    projection.Readiness.Select(MapReadiness).ToArray(),
                    normalizedKey,
                    binding?.ToString("D") ?? normalizedKey);
            }),
            cancellationToken);
    }

    /// <summary>
    /// Refuses a generic automation write staff could not confirm or clear on
    /// the Case (FRD-10): a professional finding, or a path with no staff
    /// editor on the Case. Unknown, case-owned and derived paths fall through
    /// to Core's NormalizeWritableField, which names each.
    /// </summary>
    internal static void RequireGenericWrite(string path)
    {
        if (!AssessmentVocabulary.Definitions.TryGetValue(path, out var definition)
            || AssessmentVocabulary.DerivedPaths.Contains(path))
        {
            return;
        }
        if (definition.IsFinding)
        {
            throw new McpException(
                $"The field '{path}' is a professional finding; only staff record it on the Case.");
        }
        if (!CaseWorkspaceLabels.Editors.IsStaffConfirmable(path))
        {
            throw new McpException(
                $"The field '{path}' has no staff editor on the Case, so automation cannot write it: "
                + "a value staff cannot confirm or clear would block the report.");
        }
    }

    [McpServerTool(
        Name = "pegasus_case_update_details",
        Title = "Update case details",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Ordinary case-detail editing through the same Core save path as the staff case screen: claimant, claim number, vehicle identity and mileage, accident circumstances, dates, contact, VAT status, and inspection fields. Supplied values are merged over the currently confirmed values; omitted values stay unchanged. Requires the edit lease and expected case version; the save re-opens completeness review exactly as a staff edit does. Dates are yyyy-MM-dd; inspectionMode is 'physical_address' or 'image_based_assessment' and must be saved together with inspectionAddress.")]
    public async Task<CaseUpdateDetailsToolResult> UpdateDetailsAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Why these details are being corrected (case history reason).")] string reason,
        [Description("Claimant name.")] string? claimantName = null,
        [Description("Claim number.")] string? claimNumber = null,
        [Description("Vehicle registration.")] string? vehicleRegistration = null,
        [Description("Vehicle make.")] string? vehicleMake = null,
        [Description("Vehicle model.")] string? vehicleModel = null,
        [Description("Vehicle mileage (whole number).")] long? vehicleMileage = null,
        [Description("Vehicle mileage unit, for example miles.")] string? vehicleMileageUnit = null,
        [Description("Accident circumstances.")] string? accidentCircumstances = null,
        [Description("Incident date, yyyy-MM-dd.")] string? incidentDate = null,
        [Description("Contact name.")] string? contactName = null,
        [Description("Contact email address.")] string? contactEmailAddress = null,
        [Description("Contact phone number.")] string? contactPhoneNumber = null,
        [Description("Instruction date, yyyy-MM-dd.")] string? instructionDate = null,
        [Description("VAT status text.")] string? vatStatus = null,
        [Description("Inspection date, yyyy-MM-dd; the report prints it as the date the damage was assessed.")] string? inspectionDate = null,
        [Description("Inspection deadline, yyyy-MM-dd.")] string? inspectionDeadline = null,
        [Description("Inspection address; must accompany inspectionMode.")] string? inspectionAddress = null,
        [Description("Inspection mode: physical_address or image_based_assessment.")] string? inspectionMode = null,
        [Description("Storage location for the vehicle.")] string? storageLocation = null,
        [Description("Optional Send to AI work-request identifier for round-trip correlation.")] string? workRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        var binding = ParseWorkRequestId(workRequestId);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_update_details",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            binding?.ToString("D") ?? normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (string.IsNullOrWhiteSpace(editLeaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }

                var current = await caseDataQueries.GetAsync(caseId, CaseWorkSelector.Current, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                var merged = new CaseEditableData(
                    claimantName ?? current.Claimant.Name.Confirmed?.Value,
                    claimNumber ?? current.Claim.Number.Confirmed?.Value,
                    vehicleRegistration ?? current.Vehicle.Registration.Confirmed?.Value,
                    vehicleMake ?? current.Vehicle.Make.Confirmed?.Value,
                    vehicleModel ?? current.Vehicle.Model.Confirmed?.Value,
                    vehicleMileage ?? current.Vehicle.Mileage.Confirmed?.Value,
                    vehicleMileageUnit ?? current.Vehicle.MileageUnit.Confirmed?.Value,
                    accidentCircumstances ?? current.Accident.Circumstances.Confirmed?.Value,
                    ParseDate(incidentDate, "incidentDate")
                        ?? current.Accident.IncidentDate.Confirmed?.Value,
                    contactName ?? current.Contact.Name.Confirmed?.Value,
                    contactEmailAddress ?? current.Contact.EmailAddress.Confirmed?.Value,
                    contactPhoneNumber ?? current.Contact.PhoneNumber.Confirmed?.Value,
                    ParseDate(instructionDate, "instructionDate")
                        ?? current.Instruction.InstructionDate.Confirmed?.Value,
                    vatStatus ?? current.Instruction.VatStatus.Confirmed?.Value,
                    ParseDate(inspectionDate, "inspectionDate")
                        ?? current.Inspection.InspectionDate.Confirmed?.Value,
                    ParseDate(inspectionDeadline, "inspectionDeadline")
                        ?? current.Inspection.Deadline.Confirmed?.Value,
                    inspectionAddress ?? current.Inspection.Address.Confirmed?.Value,
                    ParseInspectionMode(inspectionMode)
                        ?? current.Inspection.Mode.Confirmed?.Value,
                    current.Claimant.ContactNumber.Confirmed?.Value,
                    current.Claimant.Address.Confirmed?.Value,
                    storageLocation ?? current.Inspection.StorageLocation?.Confirmed?.Value,
                    ClaimSourceId: current.Workspace?.ClaimSource?.ClaimSourceId,
                    ClaimSourceVersion: current.Workspace?.ClaimSource?.ClaimSourceVersion,
                    ClaimSourceName: current.Workspace?.ClaimSource?.Name,
                    ClaimSourceContactName: current.Workspace?.ClaimSource?.ContactName,
                    ClaimSourceContactTelephone: current.Workspace?.ClaimSource?.ContactTelephone,
                    ClaimSourceContactEmailAddress: current.Workspace?.ClaimSource?.ContactEmailAddress,
                    ClaimSourceOverrideContactName: current.Workspace?.ClaimSource?.OverrideContactName,
                    ClaimSourceOverrideContactTelephone: current.Workspace?.ClaimSource?.OverrideContactTelephone,
                    ClaimSourceOverrideContactEmailAddress: current.Workspace?.ClaimSource?.OverrideContactEmailAddress);
                var saved = await saveCase.ExecuteAsync(
                    new(
                        caseId,
                        expectedVersion,
                        context.Actor,
                        normalizedKey,
                        reason,
                        editLeaseToken,
                        merged),
                    cancellationToken);
                return new CaseUpdateDetailsToolResult(
                    saved.Identity.CaseId,
                    saved.Version,
                    saved.State.ToString(),
                    normalizedKey,
                    binding?.ToString("D") ?? normalizedKey);
            }),
            cancellationToken);
    }

    private static AssessmentFieldToolItem MapField(AssessmentFieldValue field) => new(
        field.Path,
        field.Value,
        field.RecordedByKind.ToString(),
        field.RecordedBy,
        field.RecordedAtUtc,
        field.IsConfirmed,
        field.ConfirmedBy,
        field.ConfirmedAtUtc);

    private static EstimateLineToolItem MapLine(CaseEstimateLineRecord line) => new(
        line.Position,
        line.Type,
        line.GuideCode,
        line.Description,
        line.WorkUnits,
        line.Price,
        line.Unpriced,
        line.PartNumber,
        line.Betterment,
        line.Status,
        line.EvidenceLabel,
        line.Justification,
        line.RecordedByKind.ToString(),
        line.IsConfirmed,
        line.PaintWorkUnits,
        line.Quantity);

    private static EstimateLineInput MapLineInput(EstimateLineToolInput line) => new(
        line.Type,
        line.GuideCode,
        line.Description,
        line.WorkUnits,
        line.Price,
        line.Unpriced,
        line.PartNumber,
        line.Betterment,
        line.Status,
        line.EvidenceLabel,
        line.Justification,
        line.PaintWorkUnits,
        line.Quantity,
        line.Materials);

    private static EstimateToolItem MapEstimate(RepairSpecificationVersion estimate)
    {
        var totals = EstimateTotals.ForProjection(estimate);
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
        estimate.IsCurrent,
        estimate.CalculationBasis?.ToString());

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

    private static Guid? ParseWorkRequestId(string? workRequestId)
    {
        if (string.IsNullOrWhiteSpace(workRequestId))
        {
            return null;
        }

        return Guid.TryParse(workRequestId.Trim(), out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new McpException(
                "The work-request identifier must be a non-empty GUID when supplied.");
    }

    private static DateOnly? ParseDate(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", out var parsed)
            ? parsed
            : throw new McpException($"The {name} value must be a yyyy-MM-dd date.");
    }

    private static CaseInspectionMode? ParseInspectionMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "physical_address" => CaseInspectionMode.PhysicalAddress,
            "image_based_assessment" => CaseInspectionMode.ImageBasedAssessment,
            _ => throw new McpException(
                "The inspection mode must be physical_address or image_based_assessment.")
        };
    }
}
