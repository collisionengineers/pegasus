using System.ComponentModel;
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
    [property: Description("Whether the Case still holds an open query after the act.")] bool HasOpenQuery,
    Guid? ReplacementCaseId,
    string? ReplacementCaseReference,
    Guid? AuditWorkId,
    string? AuditReference,
    Guid? VehicleLookupWorkItemId,
    [property: Description("vehicle_history_check: recorded (the Vehicle history text now holds Experian's wording) or not_found (Experian holds no vehicle for the registration; nothing was written).")] string? VehicleHistoryCheck,
    string OperationKey);

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
    AuthoriseGarage authoriseGarage,
    UndoGarageAuthorisation undoGarageAuthorisation,
    ICreateLinkedReplacement createLinkedReplacement,
    ICloseCase closeCase,
    IReopenCase reopenCase,
    ReturnCaseToEngineer returnToEngineer,
    IArchiveCase archiveCase,
    ICreateAudit createAudit,
    IRequestVehicleLookup requestVehicleLookup,
    RunVehicleHistoryCheck runVehicleHistoryCheck,
    IRecordManualCaseChase recordManualChase,
    ICaseWorkflowQueries workflowQueries,
    TimeProvider timeProvider,
    AutomationMcpAuditor auditor,
    OneCommandEdit leases)
{
    private static readonly string[] Actions =
    [
        "hold", "release_hold", "return_to_review", "assign_engineer", "return_to_engineer",
        "close", "reopen", "archive", "create_linked_replacement", "create_audit",
        "vehicle_lookup", "vehicle_history_check", "manual_chase",
        "authorise_garage", "undo_garage_authorisation",
    ];

    [McpServerTool(
        Name = "pegasus_case_action",
        Title = "Act on case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Takes one Case lifecycle act a member of staff takes from the Case's Actions menu: hold (reason, optional reviewOn), release_hold (reason), return_to_review (reason; from With Engineer or Report sent, when the Case's own completeness facts allow), assign_engineer (optional engineerId, omitted for Unassigned; from Review, moving the Case to With Engineer, or within With Engineer; no reason, Core records who it went to), return_to_engineer (reason; from Report sent), close (reason, outcome), reopen (reason, destination), archive (reason; a closed Case whose custody is confirmed), create_linked_replacement (reason, replacementPrincipalCode; for a Case created in error), create_audit (an Inspection + Audit Case gains its Audit work), vehicle_lookup (registration; queues one DVLA/DVSA lookup), vehicle_history_check (registration; runs one Experian check of the current work's accepted registration for write-off records and replaces its Vehicle history text with the result, as the Case page's Run Experian check does; fails when Experian is not connected or cannot answer), manual_chase (chaseChannel, chaseRecipient, chaseOutcome, optional chaseNote; a Not ready Case's due work), authorise_garage (no reason; on a Garage authorisation and invoice Audit that is With Engineer with an Engineer, not yet authorised and without a total loss or cash in lieu outcome; the Case then waits Not ready for its repair invoice, or stays With Engineer when a confirmed Repair invoice is already filed) or undo_garage_authorisation (reason; once the garage is authorised; a Case waiting for its repair invoice returns to With Engineer). pegasus_case_get shows auditWorkflow and garageAuthorisedAtUtc. Queries are pegasus_case_query. A Case is never deleted. Needs the expected case version; each act ends the lease it ran under. Returns the Case's new version and state and any identifier the act created.")]
    public async Task<CaseActionToolResult> ActAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.ExpectedCaseVersion)] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs returns the same outcome.")] string operationKey,
        [Description("The act: hold, release_hold, return_to_review, assign_engineer, return_to_engineer, close, reopen, archive, create_linked_replacement, create_audit, vehicle_lookup, vehicle_history_check, manual_chase, authorise_garage or undo_garage_authorisation.")] string action,
        [Description("Why (case history reason, at most 500 characters). Required for every act except assign_engineer, create_audit, vehicle_lookup, vehicle_history_check, manual_chase and authorise_garage, which take none.")] string? reason = null,
        [Description("assign_engineer: the staff identifier of the Engineer to assign, from pegasus_vocabulary_get staff; the account must exist and be enabled. Omitted, the Case is Unassigned.")] Guid? engineerId = null,
        [Description("hold: the optional date, yyyy-MM-dd (Europe/London, today or later), to look at the Case again; it is the held Case's due date in the Work Centre. Nothing is sent on it.")] string? reviewOn = null,
        [Description("close: the terminal outcome, one of PrincipalCancelled, CollisionEngineersRejected, CreatedInError or SourceEmailUnlinked. Which outcomes the Case's state allows is Core's decision.")] string? outcome = null,
        [Description("reopen: where the closed Case goes, one of NotReady, Review, ReportPreparation (Unassigned when the Case has no Engineer) or ReportSent (needs linked report-Sent evidence). A Case created in error is never reopened.")] string? destination = null,
        [Description("create_linked_replacement: the Principal code of the corrected replacement Case.")] string? replacementPrincipalCode = null,
        [Description("vehicle_lookup and vehicle_history_check: the Case's accepted registration.")] string? registration = null,
        [Description("manual_chase: how the chase was made, for example phone or email (at most 100 characters).")] string? chaseChannel = null,
        [Description("manual_chase: who or which address was chased (at most 500 characters).")] string? chaseRecipient = null,
        [Description("manual_chase: what the chase achieved (at most 500 characters).")] string? chaseOutcome = null,
        [Description("manual_chase: an optional note about the chase (at most 1000 characters).")] string? chaseNote = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_case_action",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
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
                    // Core composes Assign Engineer's history reason.
                    // And Authorise garage's (R1161).
                    "create_audit" or "vehicle_lookup" or "vehicle_history_check" or "manual_chase" or "assign_engineer"
                        or "authorise_garage" => string.Empty,
                    _ => string.IsNullOrWhiteSpace(reason)
                        ? throw new McpException($"The {verb} action needs a reason.")
                        : reason,
                };
                var holdReview = verb == "hold" ? AutomationMcpErrors.OptionalDate(reviewOn, $"The {nameof(reviewOn)} value must be a yyyy-MM-dd date.") : null;
                var closure = verb == "close" ? ParseClosureOutcome(outcome) : default;
                var reopenTo = verb == "reopen" ? ParseReopenDestination(destination) : default;
                // An omitted engineerId hands the Case to Unassigned (#1213).
                Guid? engineer = verb == "assign_engineer" && engineerId is { } id
                    ? AutomationMcpErrors.RequireId(id, nameof(engineerId))
                    : null;
                var principalCode = verb == "create_linked_replacement"
                    ? string.IsNullOrWhiteSpace(replacementPrincipalCode)
                        ? throw new McpException("The create_linked_replacement action needs replacementPrincipalCode.")
                        : replacementPrincipalCode
                    : string.Empty;
                var lookupRegistration = verb is "vehicle_lookup" or "vehicle_history_check"
                    ? string.IsNullOrWhiteSpace(registration)
                        ? throw new McpException($"The {verb} action needs registration.")
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
                            case "authorise_garage":
                                await authoriseGarage.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token),
                                    cancellationToken);
                                return Created.None;
                            case "undo_garage_authorisation":
                                await undoGarageAuthorisation.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token),
                                    cancellationToken);
                                return Created.None;
                            case "close":
                                await closeCase.ExecuteAsync(
                                    new(caseId, expectedVersion, actor, key, reasonText, token, closure),
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
                            case "vehicle_history_check":
                                VehicleHistoryCheckOutcome checkOutcome;
                                try
                                {
                                    checkOutcome = await runVehicleHistoryCheck.ExecuteAsync(
                                        new(caseId, expectedVersion, actor, key, token, lookupRegistration),
                                        cancellationToken);
                                }
                                catch (VehicleHistoryCheckUnavailableException)
                                {
                                    throw new McpException(CaseWorkspaceLabels.Vehicle.ExperianCheckUnavailable);
                                }
                                return Created.None with
                                {
                                    VehicleHistoryCheck = checkOutcome == VehicleHistoryCheckOutcome.NotFound
                                        ? "not_found"
                                        : "recorded",
                                };
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
                    workflow.HasOpenQuery,
                    created.ReplacementCaseId,
                    created.ReplacementReference,
                    created.AuditWorkId,
                    created.AuditReference,
                    created.VehicleLookupWorkItemId,
                    created.VehicleHistoryCheck,
                    key);
            },
            cancellationToken);
    }

    private static CaseClosureOutcome ParseClosureOutcome(string? value) =>
        AutomationMcpErrors.ParseName<CaseClosureOutcome>(
            value,
            "The close action needs outcome, one of: " + string.Join(", ", Enum.GetNames<CaseClosureOutcome>()) + ".");

    private static CaseReopenDestination ParseReopenDestination(string? value) =>
        AutomationMcpErrors.ParseName<CaseReopenDestination>(
            value,
            "The reopen action needs destination, one of: " + string.Join(", ", Enum.GetNames<CaseReopenDestination>()) + ".");

    /// <summary>What an act created, beyond the Case's own new version and state.</summary>
    private sealed record Created(
        Guid? ReplacementCaseId,
        string? ReplacementReference,
        Guid? AuditWorkId,
        string? AuditReference,
        Guid? VehicleLookupWorkItemId,
        string? VehicleHistoryCheck)
    {
        public static readonly Created None = new(null, null, null, null, null, null);
    }
}
