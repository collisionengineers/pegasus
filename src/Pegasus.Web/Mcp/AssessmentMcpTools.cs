using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

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
    bool IsConfirmed);

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
    string? Justification = null);

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
/// ADR-0021 / FRD-10 (docs/adr/0021-automation-actor-direct-write-assessment-contract.md,
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
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
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
    [Description("Directly records assessment values on a case under the same edit lease and expected version as a staff save. Scalar values are keyed by the closed field-path vocabulary from the assessment screen (for example 'vehicle.condition' or 'assessment.outcome'); null clears a value; unknown paths fail closed, and case-owned paths (registration, make, model, odometer, incident/instruction dates, inspection) must be saved with pegasus_case_update_details instead. Passing estimateLines replaces the whole ordered estimate-line collection. Values written by the automation are stored unconfirmed and are reviewed by the engineer the case is assigned to; finding confirmation stays a staff Engineer act. The optional workRequestId correlates the write with a Send to AI hand-off.")]
    public async Task<AssessmentUpdateToolResult> UpdateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same result.")] string operationKey,
        [Description("Why these values are being recorded (case history reason, at most 500 characters).")] string reason,
        [Description("Scalar assessment values keyed by field path; a null value clears the field.")] Dictionary<string, string?>? fields = null,
        [Description("Full replacement for the ordered estimate-line collection; omit to leave lines untouched, pass an empty array to clear them.")] IReadOnlyList<EstimateLineToolInput>? estimateLines = null,
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
                        ?? current.Inspection.Mode.Confirmed?.Value);
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
        line.IsConfirmed);

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
        line.Justification);

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
