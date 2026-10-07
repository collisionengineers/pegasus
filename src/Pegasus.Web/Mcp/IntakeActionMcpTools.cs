using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record IntakeToolAllocation(
    Guid AttemptId,
    string Status,
    string? AttemptedCaseType,
    string? SafeReason,
    bool CanRetry,
    Guid? CaseId,
    string? CaseReference);

/// <summary>
/// One file the received item holds. A Triage reply attaches these by
/// <see cref="AssetId"/> (<c>pegasus_mail_send</c> triage_reply).
/// </summary>
internal sealed record IntakeToolFile(
    Guid AssetId,
    string FileName,
    string MediaType,
    long ContentLength,
    string CustodyState);

internal sealed record IntakeToolDetail(
    Guid ReceiptId,
    long Version,
    string SourceFileName,
    string MediaType,
    DateTimeOffset ReceivedAtUtc,
    string Decision,
    string DecisionReason,
    Guid? CaseId,
    string? CaseReference,
    string? ClassifiedCaseType,
    InstructionDraft? Draft,
    IReadOnlyList<string> MissingIdentityFields,
    IntakeToolAllocation? Allocation,
    string InspectionAddressState,
    string? InspectionAddressSuggestion,
    string? InspectionAddressResolvedValue,
    IReadOnlyList<IntakeToolFile> Files,
    string CorrelationId);

internal sealed record IntakeActionToolResult(
    Guid ReceiptId,
    string Action,
    string AllocationStatus,
    Guid? CaseId,
    string? CaseReference,
    string? AuditReference,
    string? SafeReason,
    bool IsReplay,
    string OperationKey,
    string CorrelationId);

internal sealed record CaseCreateToolResult(
    Guid CaseId,
    string Reference,
    string? AuditReference,
    string OperationKey,
    string CorrelationId);

internal enum IntakeAction
{
    Accept,
    Allocate
}

/// <summary>
/// The staff intake acts for the Automation Actor (ADR-0064, FRD-02): read one
/// received item, turn it into a Case (Create case from a received item),
/// retry a failed allocation, and create a Case directly. Each calls the same
/// Core commands as the staff Create case page and the Action Logs retry,
/// under <c>automation.intake</c>, with the resolved Automation identity.
/// Settling an inspection address stays a staff act: its record names a
/// member of staff, so an item that still needs one is refused here.
/// </summary>
[McpServerToolType]
internal sealed class IntakeActionMcpTools(
    IGetIntake getIntake,
    IResolveIntake resolveIntake,
    IAllocateIntake allocateIntake,
    IStandaloneAuditEvidenceQueries standaloneAuditEvidence,
    IInspectionAddressResolutionStore addressResolutions,
    IPrincipalInspectionModeStore principalInspectionModes,
    ICreateManualCase createManualCase,
    IContactDirectoryQueries contacts,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    /// <summary>The creation provenance the staff page records on the reviewed draft.</summary>
    private const string ReviewedDraftReason = "Case created from reviewed intake.";

    private const string ReceiptIdDescription = "The intake receipt identifier (receiptId from pegasus_intake_queue_list).";

    [McpServerTool(
        Name = "pegasus_intake_get",
        Title = "Get received item",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one received item as the Create case page reviews it: its version, processing decision, the Case it already belongs to, the classified case type, the instruction draft and which identity-critical fields it still lacks, the allocation attempt (with attemptId and whether it can be retried), the inspection address state, suggestion and settled value, and the item's files (assetId, name, type, size and custody state; a Triage reply attaches Confirmed files by assetId).")]
    public async Task<IntakeToolDetail> GetAsync(
        [Description(ReceiptIdDescription)] Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_intake_get",
            Resource(receiptId),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var receipt = await RequireReceiptAsync(receiptId, context.Actor, cancellationToken);
                var resolution = await ResolutionAsync(receipt, cancellationToken);
                return new IntakeToolDetail(
                    receipt.Id,
                    receipt.Version,
                    receipt.SourceFileName,
                    receipt.MediaType,
                    receipt.ReceivedAtUtc,
                    IntakeMcpTools.DecisionCode(receipt.Decision),
                    receipt.DecisionReason,
                    receipt.CurrentCaseId,
                    receipt.ManualAssociationVersion is null
                        ? receipt.AcceptedCaseReference
                        : receipt.ManualLinkedCaseReference,
                    receipt.MailClassificationDecision?.CaseType?.ToString(),
                    receipt.InstructionDraft,
                    receipt.InstructionDraft is { } draft
                        ? InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft)
                        : Array.Empty<string>(),
                    receipt.AllocationState is { } allocation
                        ? new IntakeToolAllocation(
                            allocation.AttemptId,
                            allocation.Status.ToString(),
                            allocation.AttemptedCaseType?.ToString(),
                            allocation.SafeReason,
                            allocation.CanRetry,
                            allocation.CaseId,
                            allocation.CaseReference)
                        : null,
                    resolution.State.ToString(),
                    resolution.Evaluation.Suggestion?.Value,
                    resolution.ResolvedValue,
                    IntakeFileIdentity.Ordered(receipt)
                        .Select(asset => new IntakeToolFile(
                            asset.Id,
                            asset.FileName,
                            asset.MediaType,
                            asset.ContentLength,
                            asset.CustodyState.ToString()))
                        .ToArray(),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_intake_action",
        Title = "Accept or allocate a received item",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One staff intake act on a received item, through the same Core commands as staff. accept turns the item into a Case, as Create case on a received item does: the reviewed draft is recorded (the draft fields named here replace the item's; an omitted field keeps its value and an empty string clears it), then the Case is allocated for principalCode (default: the draft's suggested Principal) and caseType (default: the classified type; Audit only for an item classified as an Audit). It is refused while an identity-critical field is missing, or while the inspection address still needs a staff decision (a Principal that inspects by images needs none). allocate retries a failed allocation with a reason, naming the attempt it retries. Both need the item version from pegasus_intake_get; the result names the allocation status and, once allocated, the Case.")]
    public async Task<IntakeActionToolResult> ActAsync(
        [Description(ReceiptIdDescription)] Guid receiptId,
        [Description("accept or allocate.")] string action,
        [Description("The item version from pegasus_intake_get; a stale value fails closed.")] long expectedReceiptVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key resumes the same act rather than starting another.")] string operationKey,
        [Description("accept only: the Principal code the Case is allocated for, at most 20 characters; omit to use the draft's suggested Principal.")] string? principalCode = null,
        [Description("accept only: Inspection, Audit or InspectionAndAudit; omit to use the classified case type, or Inspection when there is none.")] string? caseType = null,
        [Description("accept only: claimant name.")] string? claimantName = null,
        [Description("accept only: claim number.")] string? claimNumber = null,
        [Description("accept only: vehicle registration.")] string? vehicleRegistration = null,
        [Description("accept only: vehicle make.")] string? vehicleMake = null,
        [Description("accept only: vehicle model.")] string? vehicleModel = null,
        [Description("accept only: vehicle mileage.")] long? vehicleMileage = null,
        [Description("accept only: accident circumstances.")] string? accidentCircumstances = null,
        [Description("accept only: date of incident, yyyy-MM-dd.")] string? incidentDate = null,
        [Description("accept only: inspection date, yyyy-MM-dd; it is also the Case's accepted inspection deadline.")] string? inspectionDate = null,
        [Description("allocate only: the allocation attemptId from pegasus_intake_get that failed and is being retried.")] Guid? expectedAttemptId = null,
        [Description("allocate only: why the allocation is retried, 1 to 500 characters.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_intake_action",
            Resource(receiptId),
            key,
            () => AutomationMcpErrors.ExecuteAsync(() => IntakeRefusalsAsync(async () =>
            {
                var parsed = action?.Trim() switch
                {
                    "accept" => IntakeAction.Accept,
                    "allocate" => IntakeAction.Allocate,
                    _ => throw new McpException("action must be accept or allocate.")
                };
                IntakeAllocationResult result;
                if (parsed == IntakeAction.Allocate)
                {
                    if (new object?[]
                        {
                            principalCode, caseType, claimantName, claimNumber, vehicleRegistration, vehicleMake,
                            vehicleModel, vehicleMileage, accidentCircumstances, incidentDate, inspectionDate
                        }.Any(value => value is not null))
                    {
                        throw new McpException("allocate retries the recorded command; it takes no Principal, case type or draft field.");
                    }

                    result = await allocateIntake.RetryAsync(
                        new(
                            AutomationMcpErrors.RequireId(receiptId, "receipt identifier"),
                            expectedReceiptVersion,
                            AutomationMcpErrors.RequireId(
                                expectedAttemptId ?? throw new McpException("allocate needs expectedAttemptId."),
                                "allocation attempt identifier"),
                            context.Actor,
                            key,
                            reason ?? throw new McpException("allocate needs reason.")),
                        cancellationToken);
                }
                else
                {
                    if (expectedAttemptId is not null || reason is not null)
                    {
                        throw new McpException("accept takes no expectedAttemptId or reason; those belong to allocate.");
                    }

                    result = await AcceptAsync(
                        context.Actor,
                        receiptId,
                        expectedReceiptVersion,
                        key,
                        principalCode,
                        caseType,
                        new DraftChanges(
                            claimantName,
                            claimNumber,
                            vehicleRegistration,
                            vehicleMake,
                            vehicleModel,
                            vehicleMileage,
                            accidentCircumstances,
                            ParseDate(incidentDate, "incidentDate"),
                            incidentDate,
                            ParseDate(inspectionDate, "inspectionDate"),
                            inspectionDate),
                        cancellationToken);
                }

                return new IntakeActionToolResult(
                    receiptId,
                    parsed == IntakeAction.Accept ? "accept" : "allocate",
                    result.State.Status.ToString(),
                    result.State.CaseId,
                    result.State.CaseReference,
                    result.State.AuditReference,
                    result.State.SafeReason,
                    result.IsReplay,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            })),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_create",
        Title = "Create a Case directly",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Creates a Case directly from entered facts, as the staff Add case does, with no received item or source document. An Inspection or Inspection and Audit Case needs the identity-critical facts (claimant, claim number, registration and the rest the staff form asks for); a Triage Case takes only its Principal and the vehicle registration. An Audit is never created by hand. A Case reference is allocated once and never reused. Requires a mcp:-prefixed operation key; replaying the same key with the same facts returns the same Case.")]
    public async Task<CaseCreateToolResult> CreateCaseAsync(
        [Description("The Principal code, at most 20 characters.")] string principalCode,
        [Description("Inspection, InspectionAndAudit or Triage.")] string caseType,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Vehicle registration.")] string? vehicleRegistration = null,
        [Description("Not for Triage: claimant name.")] string? claimantName = null,
        [Description("Not for Triage: claim number.")] string? claimNumber = null,
        [Description("Not for Triage: vehicle make.")] string? vehicleMake = null,
        [Description("Not for Triage: vehicle model.")] string? vehicleModel = null,
        [Description("Not for Triage: vehicle mileage; give vehicleMileageUnit with it.")] long? vehicleMileage = null,
        [Description("Not for Triage: the mileage unit, miles or kilometres; only with vehicleMileage.")] string? vehicleMileageUnit = null,
        [Description("Not for Triage: accident circumstances.")] string? accidentCircumstances = null,
        [Description("Not for Triage: date of incident, yyyy-MM-dd.")] string? incidentDate = null,
        [Description("Not for Triage: inspection date, yyyy-MM-dd; it is also the inspection deadline.")] string? inspectionDate = null,
        [Description("Not for Triage: inspection address; the inspection mode is inferred from it as on the staff form.")] string? inspectionAddress = null,
        [Description("Not for Triage: an active Claim Source's organisation identifier from the Contacts directory.")] Guid? claimSourceId = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_create",
            principalCode?.Trim() ?? "invalid",
            key,
            () => AutomationMcpErrors.ExecuteAsync(() => IntakeRefusalsAsync(async () =>
            {
                var type = Enum.TryParse<CaseType>(caseType?.Trim(), ignoreCase: true, out var parsedType)
                    && Enum.IsDefined(parsedType)
                    && parsedType != CaseType.Audit
                        ? parsedType
                        : throw new McpException("caseType must be Inspection, InspectionAndAudit or Triage.");
                var principal = RequirePrincipalCode(principalCode);
                CaseEditableData data;
                if (type == CaseType.Triage)
                {
                    if (new object?[]
                        {
                            claimantName, claimNumber, vehicleMake, vehicleModel, vehicleMileage, vehicleMileageUnit,
                            accidentCircumstances, incidentDate, inspectionDate, inspectionAddress, claimSourceId
                        }.Any(value => value is not null))
                    {
                        throw new McpException("A Triage Case takes only principalCode and vehicleRegistration.");
                    }

                    data = new CaseEditableData(VehicleRegistration: vehicleRegistration);
                }
                else
                {
                    var claimSource = claimSourceId is { } sourceId
                        ? (await contacts.ListByRoleAsync(context.Actor, ContactRole.ClaimSource, cancellationToken))
                            .SingleOrDefault(item => item.OrganizationId == sourceId)
                            ?? throw new McpException("claimSourceId is not an active Claim Source in the directory.")
                        : null;
                    var inspection = ParseDate(inspectionDate, "inspectionDate");
                    data = new CaseEditableData(
                        claimantName,
                        claimNumber,
                        vehicleRegistration,
                        vehicleMake,
                        vehicleModel,
                        vehicleMileage,
                        vehicleMileage.HasValue ? vehicleMileageUnit : null,
                        accidentCircumstances,
                        ParseDate(incidentDate, "incidentDate"),
                        InspectionDate: inspection,
                        InspectionDeadline: inspection,
                        InspectionAddress: inspectionAddress,
                        InspectionMode: CaseDataPolicy.InferInspectionMode(inspectionAddress),
                        ClaimSourceId: claimSource?.OrganizationId,
                        ClaimSourceVersion: claimSource?.Version,
                        ClaimSourceName: claimSource?.Name,
                        ClaimSourceContactName: claimSource?.ContactPerson,
                        ClaimSourceContactTelephone: claimSource?.Telephone,
                        ClaimSourceContactEmailAddress: claimSource?.Email);
                }

                var identity = await createManualCase.ExecuteAsync(
                    new(context.Actor, key, principal, type, data),
                    cancellationToken);
                return new CaseCreateToolResult(
                    identity.CaseId,
                    identity.Reference,
                    identity.AuditReference,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            })),
            cancellationToken);
    }

    /// <summary>
    /// Create case on a received item, step for step as the staff page runs
    /// it: the reviewed draft, then the acceptance at the version that draft
    /// left. A replay of the same key replays the draft and resumes.
    /// </summary>
    private async Task<IntakeAllocationResult> AcceptAsync(
        ActionActor actor,
        Guid receiptId,
        long expectedReceiptVersion,
        string key,
        string? principalCode,
        string? caseType,
        DraftChanges changes,
        CancellationToken cancellationToken)
    {
        var receipt = await RequireReceiptAsync(receiptId, actor, cancellationToken);
        if ((receipt.AcceptedCaseId ?? receipt.CurrentCaseId) is not null)
        {
            throw new McpException("This item already has a case.");
        }
        if (receipt.Decision is not (IntakeDecision.OcrRequired or IntakeDecision.NeedsSorting)
            && !IntakeDecisionPolicy.CanBecomeCase(receipt.Decision))
        {
            throw new McpException(OperatorLabels.IntakeCannotBecomeCaseReason(receipt.Decision));
        }

        var classifiedType = receipt.MailClassificationDecision?.CaseType;
        var type = caseType is null
            ? classifiedType ?? CaseType.Inspection
            : Enum.TryParse<CaseType>(caseType.Trim(), ignoreCase: true, out var parsedType)
                && Enum.IsDefined(parsedType)
                && parsedType != CaseType.Triage
                    ? parsedType
                    : throw new McpException("caseType must be Inspection, Audit or InspectionAndAudit.");
        if (type == CaseType.Audit && classifiedType != CaseType.Audit)
        {
            throw new McpException("Only an item classified as an Audit becomes an Audit case.");
        }

        var current = receipt.InstructionDraft
            ?? new InstructionDraft(null, null, null, null, null, null, null, null, null, null);
        var principal = RequirePrincipalCode(principalCode ?? current.SuggestedPrincipalCode);
        var principalIsImageBased = await principalInspectionModes.GetForPrincipalAsync(principal, cancellationToken)
            == CaseInspectionMode.ImageBasedAssessment;
        var resolution = await ResolutionAsync(receipt, cancellationToken);
        if (!InspectionAddressResolutionPolicy.SatisfiesCaseCreation(resolution.State, principalIsImageBased))
        {
            throw new McpException(
                "The inspection address still needs a staff decision on the Create case page before this item can become a case.");
        }

        var draft = current with
        {
            SuggestedPrincipalCode = current.SuggestedPrincipalCode ?? principal,
            ClaimantName = Text(changes.ClaimantName, current.ClaimantName),
            ClaimNumber = Text(changes.ClaimNumber, current.ClaimNumber),
            VehicleRegistration = Text(changes.VehicleRegistration, current.VehicleRegistration),
            VehicleMake = Text(changes.VehicleMake, current.VehicleMake),
            VehicleModel = Text(changes.VehicleModel, current.VehicleModel),
            VehicleMileage = changes.VehicleMileage ?? current.VehicleMileage,
            AccidentCircumstances = Text(changes.AccidentCircumstances, current.AccidentCircumstances),
            DateOfIncident = changes.IncidentDateText is null ? current.DateOfIncident : changes.IncidentDate,
            InspectionDate = changes.InspectionDateText is null ? current.InspectionDate : changes.InspectionDate,
            // The address the page puts in the draft: the Principal's own
            // image-based mode, or the address a member of staff settled.
            InspectionAddress = principalIsImageBased
                ? resolution.ResolvedValue
                    ?? resolution.Evaluation.Suggestion?.Value
                    ?? Ext18InspectionAddressPolicy.ImageBasedAssessment
                : resolution.ResolvedValue
        };
        var missing = InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft);
        if (missing.Count > 0)
        {
            throw new McpException(
                $"{string.Join(", ", missing)} is needed before this item can become a case.");
        }

        _ = await resolveIntake.ExecuteAsync(
            new(
                receipt.Id,
                expectedReceiptVersion,
                actor,
                AutomationEditLease.Derive(key, ":draft"),
                ReviewedDraftReason,
                IntakeResolutionKind.CorrectDraft,
                draft),
            cancellationToken);
        var auditEvidence = await standaloneAuditEvidence.GetForReceiptAsync(receipt.Id, cancellationToken);
        return await allocateIntake.AttemptStaffCreateAsync(
            new(
                receipt.Id,
                // The draft correction advances the reviewed version once,
                // whether newly applied or replayed, as on the staff page.
                expectedReceiptVersion + 1,
                actor,
                key,
                type,
                principal,
                new CaseCompleteness(
                    InstructionComplete: true,
                    ImagesComplete: InstructionEvidenceImages.Select(receipt.AssetRecords).Count > 0),
                auditEvidence is { IntakeReceiptId: var evidenceReceiptId } && evidenceReceiptId == receipt.Id
                    ? auditEvidence.Id
                    : null,
                draft.InspectionDate),
            cancellationToken);
    }

    private async Task<IntakeReceipt> RequireReceiptAsync(
        Guid receiptId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        await getIntake.ExecuteAsync(
            new(AutomationMcpErrors.RequireId(receiptId, "receipt identifier"), actor),
            cancellationToken)
        ?? throw new McpException("The received item was not found.");

    private async Task<InspectionAddressResolutionSnapshot> ResolutionAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken) =>
        await addressResolutions.GetAsync(receipt.Id, cancellationToken)
        ?? new(
            receipt.Id,
            receipt.Version,
            InspectionAddressResolutionState.Unresolved,
            Ext18InspectionAddressPolicy.Evaluate(receipt),
            null,
            null,
            null);

    /// <summary>
    /// The intake refusals carry safe, fixed sentences of their own; they are
    /// not argument or state exceptions, so they are named here rather than
    /// collapsing to a generic failure.
    /// </summary>
    private static async Task<TResult> IntakeRefusalsAsync<TResult>(Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (exception is IntakeVersionConflictException
            or IntakeAllocationConcurrencyException)
        {
            throw new McpException("The received item changed since it was read; reload it with pegasus_intake_get and retry.");
        }
        catch (Exception exception) when (exception is IntakeOperationConflictException
            or IntakeAllocationOperationConflictException
            or CaseAcceptanceOperationConflictException)
        {
            throw new McpException("The operation key was already used with different details.");
        }
        catch (PrincipalUnavailableException)
        {
            throw new McpException("The Principal is unavailable.");
        }
    }

    private static string RequirePrincipalCode(string? principalCode)
    {
        var normalized = principalCode?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(normalized)
            ? throw new McpException("A principal code is required.")
            : normalized.Length > CasePrincipalCode.MaximumLength
                ? throw new McpException($"The principal code must be {CasePrincipalCode.MaximumLength} characters or fewer.")
                : normalized;
    }

    /// <summary>Omitted keeps the current value; an empty string clears it.</summary>
    private static string? Text(string? submitted, string? current) =>
        submitted is null ? current : string.IsNullOrWhiteSpace(submitted) ? null : submitted.Trim();

    private static DateOnly? ParseDate(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : throw new McpException($"{name} must be a yyyy-MM-dd date.");

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    /// <summary>The draft fields an accept names, with each date's raw text so an empty string clears.</summary>
    private sealed record DraftChanges(
        string? ClaimantName,
        string? ClaimNumber,
        string? VehicleRegistration,
        string? VehicleMake,
        string? VehicleModel,
        long? VehicleMileage,
        string? AccidentCircumstances,
        DateOnly? IncidentDate,
        string? IncidentDateText,
        DateOnly? InspectionDate,
        string? InspectionDateText);
}
