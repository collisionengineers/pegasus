using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record CaseActionToolResult(
    Guid CaseId,
    string Action,
    long CaseVersion,
    string State,
    Guid? ReplacementCaseId,
    string? ReplacementCaseReference,
    Guid? AuditWorkId,
    string? AuditReference,
    Guid? VehicleLookupWorkItemId,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor Case lifecycle (FRD-10, ADR-0064): the Actions-menu acts a
/// member of staff takes on a Case, each through the same Core command, edit
/// lease, version and operation-key guards as the staff Workflow, Closure,
/// Tasks and Vehicle pages and the Case page's Create audit. The Automation
/// Actor does anything a staff member can on the Case (operator, 7 October
/// 2026); Core alone decides which act a state allows.
/// </summary>
[McpServerToolType]
internal sealed class CaseLifecycleMcpTools(
    IHoldCase holdCase,
    IReleaseCase releaseCase,
    ITransitionCase transitionCase,
    IAssignCaseEngineer assignEngineer,
    ICreateLinkedReplacement createLinkedReplacement,
    ICloseCase closeCase,
    IReopenCase reopenCase,
    IReturnCaseToEngineer returnToEngineer,
    IArchiveCase archiveCase,
    ICreateAudit createAudit,
    IRequestVehicleLookup requestVehicleLookup,
    IRecordManualCaseChase recordManualChase,
    ICaseWorkflowQueries workflowQueries,
    TimeProvider timeProvider,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    private static readonly string[] Actions =
    [
        "hold", "release_hold", "return_to_review", "assign_engineer", "return_to_engineer",
        "close", "complete", "reopen", "archive", "create_linked_replacement", "create_audit",
        "vehicle_lookup", "manual_chase",
    ];

    [McpServerTool(
        Name = "pegasus_case_action",
        Title = "Act on case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Takes one Case lifecycle act a member of staff takes from the Case's Actions menu, through the same Core command and guards: hold (reason, optional reviewOn), release_hold (reason), return_to_review (reason; from Not ready, when the Case's own completeness facts allow), assign_engineer (engineerId; from Review, moves the Case to With Engineer), return_to_engineer (reason; from Completed or Query), close (reason, outcome), complete (reason; records the Completed outcome), reopen (reason, destination), archive (reason; a closed Case whose custody is confirmed), create_linked_replacement (reason, replacementPrincipalCode; for a Case created in error), create_audit (an Inspection + Audit Case gains its Audit work), vehicle_lookup (registration; queues one DVLA/DVSA lookup) or manual_chase (chaseChannel, chaseRecipient, chaseOutcome, optional chaseNote; a Not ready Case's due work). A Case is never deleted. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command); each act ends the lease it ran under. Returns the Case's new version and state and any identifier the act created.")]
    public async Task<CaseActionToolResult> ActAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs returns the same outcome.")] string operationKey,
        [Description("The act: hold, release_hold, return_to_review, assign_engineer, return_to_engineer, close, complete, reopen, archive, create_linked_replacement, create_audit, vehicle_lookup or manual_chase.")] string action,
        [Description("Why (case history reason, at most 500 characters). Required for every act except assign_engineer (which records 'Assign Engineer' when omitted), create_audit, vehicle_lookup and manual_chase, which take none.")] string? reason = null,
        [Description("assign_engineer: the staff identifier of the Engineer to assign; the account must exist and be enabled.")] Guid? engineerId = null,
        [Description("hold: the optional date, yyyy-MM-dd (Europe/London, today or later), to look at the Case again; it is the held Case's due date in the Work Centre. Nothing is sent on it.")] string? reviewOn = null,
        [Description("close: the terminal outcome, one of PrincipalCancelled, CollisionEngineersRejected, CreatedInError, SourceEmailUnlinked or PostReportComplete. Which outcomes the Case's state allows is Core's decision.")] string? outcome = null,
        [Description("reopen: where the closed Case goes, one of NotReady, Review, ReportPreparation (needs an assigned Engineer) or PostReport (needs linked report-Sent evidence). A Case created in error is never reopened.")] string? destination = null,
        [Description("create_linked_replacement: the Principal code of the corrected replacement Case.")] string? replacementPrincipalCode = null,
        [Description("vehicle_lookup: the registration to look up.")] string? registration = null,
        [Description("manual_chase: how the chase was made, for example phone or email (at most 100 characters).")] string? chaseChannel = null,
        [Description("manual_chase: who or which address was chased (at most 500 characters).")] string? chaseRecipient = null,
        [Description("manual_chase: what the chase achieved (at most 500 characters).")] string? chaseOutcome = null,
        [Description("manual_chase: an optional note about the chase (at most 1000 characters).")] string? chaseNote = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_action",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var verb = action?.Trim() ?? string.Empty;
                if (!Actions.Contains(verb, StringComparer.Ordinal))
                {
                    throw new McpException("The action must be one of: " + string.Join(", ", Actions) + ".");
                }

                // Every input is checked before the lease is claimed, so a
                // malformed call claims nothing.
                var reasonText = verb switch
                {
                    "create_audit" or "vehicle_lookup" or "manual_chase" => string.Empty,
                    "assign_engineer" => string.IsNullOrWhiteSpace(reason) ? CaseWorkspaceLabels.AssignEngineer : reason,
                    _ => string.IsNullOrWhiteSpace(reason)
                        ? throw new McpException($"The {verb} action needs a reason.")
                        : reason,
                };
                var holdReview = verb == "hold" ? ParseDate(reviewOn, nameof(reviewOn)) : null;
                var closure = verb == "close" ? ParseClosureOutcome(outcome) : default;
                var reopenTo = verb == "reopen" ? ParseReopenDestination(destination) : default;
                var engineer = verb == "assign_engineer"
                    ? engineerId is { } id && id != Guid.Empty
                        ? id
                        : throw new McpException("The assign_engineer action needs engineerId.")
                    : Guid.Empty;
                var principalCode = verb == "create_linked_replacement"
                    ? string.IsNullOrWhiteSpace(replacementPrincipalCode)
                        ? throw new McpException("The create_linked_replacement action needs replacementPrincipalCode.")
                        : replacementPrincipalCode
                    : string.Empty;
                var lookupRegistration = verb == "vehicle_lookup"
                    ? string.IsNullOrWhiteSpace(registration)
                        ? throw new McpException("The vehicle_lookup action needs registration.")
                        : registration
                    : string.Empty;
                if (verb == "manual_chase"
                    && (string.IsNullOrWhiteSpace(chaseChannel)
                        || string.IsNullOrWhiteSpace(chaseRecipient)
                        || string.IsNullOrWhiteSpace(chaseOutcome)))
                {
                    throw new McpException("The manual_chase action needs chaseChannel, chaseRecipient and chaseOutcome.");
                }

                var actor = context.Actor;
                var created = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    actor,
                    key,
                    async token =>
                    {
                        switch (verb)
                        {
                            case "hold":
                                await holdCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token, holdReview),
                                    cancellationToken);
                                return Created.None;
                            case "release_hold":
                                await releaseCase.ExecuteAsync(
                                    new ChangeCaseStateRequest(caseId, expectedVersion, actor, key, reasonText, token),
                                    cancellationToken);
                                return Created.None;
                            case "return_to_review":
                                await transitionCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token,
                                        CaseTransitionDestination.Review),
                                    cancellationToken);
                                return Created.None;
                            case "assign_engineer":
                                await assignEngineer.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token, engineer),
                                    cancellationToken);
                                return Created.None;
                            case "return_to_engineer":
                                await returnToEngineer.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token),
                                    cancellationToken);
                                return Created.None;
                            case "close":
                            case "complete":
                                await closeCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token,
                                        verb == "complete" ? CaseClosureOutcome.PostReportComplete : closure),
                                    cancellationToken);
                                return Created.None;
                            case "reopen":
                                await reopenCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token, reopenTo),
                                    cancellationToken);
                                return Created.None;
                            case "archive":
                                await archiveCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token),
                                    cancellationToken);
                                return Created.None;
                            case "create_linked_replacement":
                                var replacement = await createLinkedReplacement.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token, principalCode),
                                    cancellationToken);
                                return Created.None with
                                {
                                    ReplacementCaseId = replacement.Identity.CaseId,
                                    ReplacementReference = replacement.Identity.Reference,
                                };
                            case "create_audit":
                                CreateAuditResult audit;
                                try
                                {
                                    audit = await createAudit.ExecuteAsync(
                                        new(caseId, expectedVersion, actor, key, token),
                                        cancellationToken);
                                }
                                catch (AuditCreationException refusal)
                                {
                                    // Core's refusal names why, as the Case page shows it.
                                    throw new McpException(refusal.Message);
                                }
                                return Created.None with
                                {
                                    AuditWorkId = audit.AuditWorkId,
                                    AuditReference = audit.AuditReference,
                                };
                            case "vehicle_lookup":
                                var lookup = await requestVehicleLookup.ExecuteAsync(
                                    new(caseId, expectedVersion, lookupRegistration, actor, key, token),
                                    cancellationToken);
                                return Created.None with { VehicleLookupWorkItemId = lookup.WorkItemId };
                            default:
                                // The attempt time is the server's, as the staff
                                // Tasks page records it: a chase is recorded as
                                // it is asserted.
                                await recordManualChase.ExecuteAsync(
                                    new(caseId, expectedVersion, token, actor, key, chaseChannel!, chaseRecipient!,
                                        timeProvider.GetUtcNow(), chaseOutcome!, chaseNote),
                                    cancellationToken);
                                return Created.None;
                        }
                    },
                    cancellationToken);
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new CaseActionToolResult(
                    caseId,
                    verb,
                    workflow.Version,
                    workflow.State.ToString(),
                    created.ReplacementCaseId,
                    created.ReplacementReference,
                    created.AuditWorkId,
                    created.AuditReference,
                    created.VehicleLookupWorkItemId,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    private static DateOnly? ParseDate(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
                ? parsed
                : throw new McpException($"The {name} value must be a yyyy-MM-dd date.");

    private static CaseClosureOutcome ParseClosureOutcome(string? value) =>
        Enum.TryParse<CaseClosureOutcome>(value?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? parsed
            : throw new McpException(
                "The close action needs outcome, one of: " + string.Join(", ", Enum.GetNames<CaseClosureOutcome>()) + ".");

    private static CaseReopenDestination ParseReopenDestination(string? value) =>
        Enum.TryParse<CaseReopenDestination>(value?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? parsed
            : throw new McpException(
                "The reopen action needs destination, one of: " + string.Join(", ", Enum.GetNames<CaseReopenDestination>()) + ".");

    /// <summary>What an act created, beyond the Case's own new version and state.</summary>
    private sealed record Created(
        Guid? ReplacementCaseId,
        string? ReplacementReference,
        Guid? AuditWorkId,
        string? AuditReference,
        Guid? VehicleLookupWorkItemId)
    {
        public static readonly Created None = new(null, null, null, null, null);
    }
}
