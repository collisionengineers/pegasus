using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

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
    int? Quantity = null);

internal sealed record EstimateTotalsToolItem(
    decimal Parts,
    decimal Labour,
    decimal Paint,
    decimal Other,
    decimal Subtotal,
    decimal VatPercent,
    decimal Vat,
    decimal Total);

internal sealed record EstimateToolItem(
    Guid EstimateId,
    int Version,
    string Name,
    string State,
    string SourceRoute,
    bool IsCurrent,
    Guid? AiJobId,
    int? RepairDays,
    decimal? LabourRate,
    decimal? PaintLabourRate,
    decimal? PaintMaterials,
    decimal? OtherCosts,
    decimal VatPercent,
    string? Notes,
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

internal sealed record EstimateListToolResult(
    Guid CaseId,
    IReadOnlyList<EstimateToolItem> Estimates,
    string CorrelationId);

internal sealed record AssessmentCaseOwnedToolData(
    string? Registration,
    string? Make,
    string? Model,
    long? Mileage,
    string? MileageUnit,
    string? IncidentDate,
    string? InstructionDate,
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
/// review. Structurally absent, on purpose: any finding-confirmation tool,
/// any report-approval tool, and any tool that dispatches anything outward.
/// </summary>
[McpServerToolType]
internal sealed class AssessmentMcpTools(
    IGetCaseAssessment getAssessment,
    ISaveAssessment saveAssessment,
    ICaseDataQueries caseDataQueries,
    ISaveCase saveCase,
    ISaveEstimate saveEstimate,
    IListCaseEstimates listEstimates,
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
        string sourceRoute,
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
                if (!Enum.TryParse<RepairSpecificationSourceRoute>(sourceRoute, true, out var route)
                    || !Enum.IsDefined(route)
                    || !RepairSpecificationPolicy.IsDocumentRoute(route))
                {
                    throw new McpException("The estimate source route is not importable.");
                }
                var importer = importRawEstimate
                    ?? throw new McpException("Estimate import is unavailable in this runtime.");
                var estimateId = await importer.ExecuteAsync(
                    new(context.Actor, caseId, expectedVersion, editLeaseToken,
                        occurrenceId, documentVersionId, sha256, route, key, name),
                    cancellationToken);
                return new EstimateImportToolResult(
                    caseId, estimateId, name.Trim(), key,
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
        [Description("Repair days.")] int? repairDays = null,
        [Description("Labour rate per hour.")] decimal? labourRate = null,
        [Description("Paint labour rate per hour.")] decimal? paintLabourRate = null,
        [Description("Paint materials amount.")] decimal? paintMaterials = null,
        [Description("Other costs amount.")] decimal? otherCosts = null,
        [Description("VAT percentage, 0 to 100; defaults to 20.")] decimal? vatPercent = null,
        [Description("Free-text notes (at most 4000 characters).")] string? notes = null,
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
                            repairDays,
                            labourRate,
                            paintLabourRate,
                            paintMaterials,
                            otherCosts,
                            vatPercent ?? EstimatePolicy.DefaultVatPercent,
                            notes),
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
    [Description("Lists every estimate on a case in version order with its state (Draft, Accepted, Superseded, Discarded), source route, whether it is the Current estimate, the AI job it cites, its header, lines and totals computed by Pegasus.")]
    public async Task<EstimateListToolResult> ListEstimatesAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
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
                var estimates = await listEstimates.ExecuteAsync(caseId, cancellationToken);
                return new EstimateListToolResult(
                    caseId,
                    estimates.Select(MapEstimate).ToArray(),
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
    [Description("Returns the recorded assessment surface for one case: every recorded field value with provenance and its confirmed/unconfirmed mark, the ordered estimate lines, the case-owned fields the assessment reads (registration, make, model, mileage, dates, inspection), and the readiness list naming what is still outstanding.")]
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
    [Description("Records ordinary non-finding assessment draft fields under the case edit lease and expected version. Finding, valuation, estimate, signatory and case-owned fields are refused and must use their named commands. Values written by automation remain unconfirmed. The optional workRequestId correlates the write with a Send to AI hand-off.")]
    public async Task<AssessmentUpdateToolResult> UpdateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why these values are being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("Scalar assessment values keyed by field path; a null value clears the field.")] Dictionary<string, string?>? fields = null,
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
                    if (AssessmentVocabulary.Definitions.TryGetValue(path, out var definition)
                        && definition.IsFinding)
                    {
                        throw new McpException(
                            $"The field '{path}' is owned by a named professional command.");
                    }
                    if (IsEstimateOwnedField(path))
                    {
                        throw new McpException(
                            $"The field '{path}' is owned by a named estimate command.");
                    }
                    if (IsSignatoryField(path))
                    {
                        throw new McpException(
                            $"The field '{path}' is owned by a named signatory command.");
                    }
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

    private static bool IsSignatoryField(string path) => path is
        AssessmentVocabulary.EngineerName
        or AssessmentVocabulary.EngineerQualifications
        or AssessmentVocabulary.EngineerSignature;

    private static bool IsEstimateOwnedField(string path) => path is
        AssessmentVocabulary.RateCard
        or AssessmentVocabulary.RateClass
        or AssessmentVocabulary.RateManufacturerApproved
        or AssessmentVocabulary.RateRegionalUplift
        or AssessmentVocabulary.CostRecoveryCharge
        or AssessmentVocabulary.CostStorageCharge
        or AssessmentVocabulary.CostRepairerVatRegistered;

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
        [Description("Inspection date, yyyy-MM-dd.")] string? inspectionDate = null,
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

                var current = await caseDataQueries.GetAsync(caseId, cancellationToken)
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
                    storageLocation ?? current.Inspection.StorageLocation?.Confirmed?.Value);
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
        line.Quantity);

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
            details.RepairDays,
            details.LabourRate,
            details.PaintLabourRate,
            details.PaintMaterials,
            details.OtherCosts,
            details.VatPercent,
            details.Notes,
            estimate.Lines.Select(MapLine).ToArray(),
            new(totals.Parts, totals.Labour, totals.Paint, totals.Other,
                totals.Subtotal, totals.VatPercent, totals.Vat, totals.Total),
            estimate.CreatedBy,
            estimate.CreatedAtUtc);
    }

    private static AssessmentCaseOwnedToolData MapCaseOwned(AssessmentCaseOwnedData data) => new(
        data.Registration,
        data.Make,
        data.Model,
        data.Mileage,
        data.MileageUnit,
        data.IncidentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        data.InstructionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
