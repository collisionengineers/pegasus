using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal enum EditLeaseRecordKind
{
    Case,
    Triage,
    ImageIntake
}

internal sealed record EditLeaseToolResult(
    string RecordKind,
    Guid RecordId,
    string? Reference,
    string EditLeaseToken,
    string Holder,
    long Version,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey);

internal sealed record EditLeaseReleaseToolResult(
    string RecordKind,
    Guid RecordId,
    bool Released,
    string OperationKey);

/// <summary>
/// The explicit edit lease for multi-step automation work on a Case, a Triage
/// Case or an Image Intake record: the same server-owned lease staff editing
/// claims, through the same Core ports and the same rules for every kind (R515
/// to R519): a claim is
/// made at the record's current version and refused only for a newer one, a
/// replayed claim returns the same token, and a lapsed lease nobody claimed
/// since carries on. With <c>takeOver</c> on a Case, <c>pegasus_edit_begin</c>
/// asks the member of staff holding the lease to hand it over and waits inside
/// the call for the answer (R554, 30 October 2026); the Automation Actor never
/// forces a hand-over. With <c>takeOver</c> on a Triage Case or an Image Intake
/// record it takes over a staff scope at once (R556). Never over another
/// Automation session's lease; nothing here merges another holder's edit. A
/// Case lease needs <c>automation.cases</c>, a Triage or Image Intake lease
/// <c>automation.intake</c>, as the records' own tools do. The lease stands
/// through every write made under it until <c>pegasus_edit_end</c> releases it
/// (operator, 8 October 2026). A single write needs none of this: a write tool
/// given no token holds the lease for that one command
/// (<see cref="Pegasus.Web.Presentation.OneCommandEdit"/>).
/// <para>
/// For a member of staff signed in from Pegasus Desktop the staff rules of
/// FRD-14 apply instead: the call resumes a lease that person already holds,
/// and a write made under a staff lease ends it, so a token carries one write.
/// </para>
/// </summary>
[McpServerToolType]
internal sealed class EditLeaseMcpTools(
    CaseEditLeases caseLeases,
    CaseEditRequestWatch requestWatch,
    IEditScopeLeases editScopes,
    IGetTriage getTriage,
    IImageIntakeQueries imageIntakes,
    Pegasus.Web.Presentation.OneCommandEdit oneCommandEdit,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const string RecordKindDescription = "Case, Triage or ImageIntake: which record's lease.";

    private const string RecordIdDescription = "The Case identifier, the Triage Case identifier, or the Image Intake record identifier.";

    [McpServerTool(
        Name = "pegasus_edit_begin",
        Title = "Begin edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Claims the server-owned five-minute edit lease on one Case, Triage Case or Image Intake record for multi-step work, using the same guard as staff editing. The lease is issued at the record's current version, which the result returns. The token carries every write made under it: present it on each write until pegasus_edit_end releases it. When a member of staff is editing the Case (a write or a claim is refused because edit authority is already held), call this again with takeOver true: the member of staff is shown the request and can hand the Case over, and the call waits up to 45 seconds for their answer and returns the lease if they accept. That request is how the Automation Actor gets a Case a member of staff holds; it is a request they can decline, not a force. A refusal names declined, no answer or already asked; ask again later. A lease another Automation session holds is not requested: takeOver then claims as a plain claim would. With takeOver on a Triage Case or an Image Intake record it takes over a staff scope at once, and the takeover is recorded in the record's history. The claim fails closed when the expected version is newer than the record's. A single write needs no lease call: every write tool takes the lease itself for that one command when editLeaseToken is omitted.")]
    public async Task<EditLeaseToolResult> BeginAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description(RecordIdDescription)] Guid recordId,
        [Description("The record version the caller observed; a version newer than the record's fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs returns the same lease token while that lease is live.")] string operationKey,
        [Description("Set true when a member of staff holds the Case: asks them to hand it over and waits up to 45 seconds for their answer (a request they can decline, not a force). On a Triage Case or an Image Intake record: takes over a staff scope at once. Leave false to claim only a free record.")] bool takeOver = false,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_begin", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_edit_begin",
            AutomationMcpAuditor.Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(context, async () =>
            {
                AutomationMcpErrors.RequireId(recordId, "record identifier");
                if (kind == EditLeaseRecordKind.Case)
                {
                    // A member of staff who already holds the Case is handed the lease they
                    // hold, as their Case page is; nobody takes over their own lease.
                    var lease = await oneCommandEdit.ResumeOwnCaseLeaseAsync(recordId, context.Actor, cancellationToken)
                        ?? (takeOver
                            ? await AskForCaseLeaseAsync(recordId, expectedVersion, context.Actor, normalizedKey, cancellationToken)
                            : await caseLeases.ClaimAsync(
                                new(recordId, expectedVersion, context.Actor, normalizedKey),
                                cancellationToken));
                    return new EditLeaseToolResult(
                        kind.ToString(), recordId, null, lease.Token, lease.Holder, lease.Version,
                        lease.ExpiresAtUtc, normalizedKey);
                }

                var scopeLease = await editScopes.ClaimAsync(
                    new(ScopeKind(kind), recordId, expectedVersion, context.Actor, normalizedKey)
                    {
                        TakeOver = takeOver
                    },
                    cancellationToken);
                return new EditLeaseToolResult(
                    kind.ToString(), recordId, await ScopeReferenceAsync(kind, recordId, context, cancellationToken),
                    scopeLease.Token, scopeLease.Holder, scopeLease.RecordVersion, scopeLease.ExpiresAtUtc,
                    normalizedKey);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_edit_renew",
        Title = "Renew edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Extends the edit lease claimed with pegasus_edit_begin so work that outlasts five minutes continues without re-claiming. A renewal is a heartbeat, for every record kind, and takes no version; it returns the same token. A lease that lapsed with nobody claiming the record since carries on. Fails closed for a non-holder, or once another editor has claimed the record.")]
    public async Task<EditLeaseToolResult> RenewAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description(RecordIdDescription)] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_renew", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);

        // Routine renewal is telemetry, not permanent history; only the refusal is material.
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_edit_renew",
            AutomationMcpAuditor.Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(context, async () =>
            {
                AutomationMcpErrors.RequireId(recordId, "record identifier");
                var token = RequireToken(editLeaseToken);
                if (kind == EditLeaseRecordKind.Case)
                {
                    var lease = await caseLeases.HeartbeatAsync(
                        new(recordId, context.Actor, token),
                        cancellationToken);
                    return new EditLeaseToolResult(
                        kind.ToString(), recordId, null, lease.Token, lease.Holder, lease.Version,
                        lease.ExpiresAtUtc, normalizedKey);
                }

                var scopeLease = await editScopes.HeartbeatAsync(
                    new(ScopeKind(kind), recordId, context.Actor, token),
                    cancellationToken);
                return new EditLeaseToolResult(
                    kind.ToString(), recordId, await ScopeReferenceAsync(kind, recordId, context, cancellationToken),
                    scopeLease.Token, scopeLease.Holder, scopeLease.RecordVersion, scopeLease.ExpiresAtUtc,
                    normalizedKey);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_edit_end",
        Title = "End edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Releases an edit lease claimed with pegasus_edit_begin, so the record is free for colleagues at once. A lease that has already been released answers released false; one that lapsed with nobody claiming the record since is released.")]
    public async Task<EditLeaseReleaseToolResult> EndAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description(RecordIdDescription)] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_end", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_edit_end",
            AutomationMcpAuditor.Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(context, async () =>
            {
                AutomationMcpErrors.RequireId(recordId, "record identifier");
                var token = RequireToken(editLeaseToken);
                // A lease that is already gone leaves nothing to end.
                var released = true;
                try
                {
                    if (kind == EditLeaseRecordKind.Case)
                    {
                        await caseLeases.ReleaseAsync(
                            new(recordId, context.Actor, normalizedKey, token),
                            cancellationToken);
                    }
                    else
                    {
                        await editScopes.ReleaseAsync(
                            new(ScopeKind(kind), recordId, context.Actor, normalizedKey, token),
                            cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is CaseEditLeaseExpiredException or EditScopeExpiredException)
                {
                    released = false;
                }
                return new EditLeaseReleaseToolResult(
                    kind.ToString(), recordId, released, normalizedKey);
            }),
            cancellationToken);
    }

    /// <summary>
    /// Asks the member of staff holding the Case lease to hand it over and waits for the answer
    /// inside the call (R554, 30 October 2026). A free Case is claimed as usual, and so is a Case
    /// another Automation session holds, which refuses the claim as it always has. The Automation
    /// Actor never forces: a decline or silence is a refusal. A call sent again under the same key
    /// rejoins its request, since the store answers the request's state.
    /// </summary>
    private async Task<CaseEditLease> AskForCaseLeaseAsync(
        Guid caseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var askedAgain = false;
        while (true)
        {
            var raised = await caseLeases.RaiseRequestAsync(
                new(caseId, expectedVersion, actor, operationKey),
                cancellationToken);
            if (raised.Outcome == CaseEditRequestOutcome.Accepted && raised.Lease is { } accepted)
            {
                return accepted;
            }
            if (raised.Outcome != CaseEditRequestOutcome.Pending || raised.Request is not { } request)
            {
                // Free, or held by another Automation session (Held, when it shares this client's
                // subject; the Automation Actor never resumes): the plain claim takes a free lease
                // and refuses a held one with the existing lease-conflict refusal.
                return await caseLeases.ClaimAsync(
                    new(caseId, expectedVersion, actor, operationKey),
                    cancellationToken);
            }

            var (handed, version) = await AwaitAnswerAsync(caseId, actor, request, cancellationToken);
            if (handed is not null)
            {
                return handed;
            }

            // The request ended without an answer to it (the holder finished, or another claim
            // landed): ask once more under the same key; a second ending is a held lease.
            if (askedAgain)
            {
                throw new CaseEditLeaseConflictException(caseId, version);
            }
            askedAgain = true;
        }
    }

    /// <summary>
    /// Waits on the Case's long poll until the request is answered, ends or goes unanswered
    /// through its window. Answers the handed lease when accepted, or no lease when the request
    /// ended. A call cut off while the request is waiting withdraws it.
    /// </summary>
    private async Task<(CaseEditLease? Lease, long Version)> AwaitAnswerAsync(
        Guid caseId,
        ActionActor actor,
        CaseEditRequestSnapshot request,
        CancellationToken cancellationToken)
    {
        string? stamp = null;
        try
        {
            while (true)
            {
                var view = await requestWatch.WaitAsync(
                    caseId, actor, stamp, CaseEditRequestWatch.MaximumHold, cancellationToken);
                stamp = view.Stamp;
                // Only this request's acceptance hands the call a lease; a lease another session
                // of the same client claimed is never this call's.
                if (view.OwnLease is { } lease
                    && view.Request is { Answer: CaseEditRequestAnswer.Accepted } accepted
                    && accepted.Id == request.Id)
                {
                    return (lease, view.Version);
                }
                // Only the request's own state ends the wait. A lease that is momentarily free
                // is the holder's save ending and re-claiming it; acting on that would take the
                // lease out from under them, so the wait sits it out inside the window.
                if (view.Request is not { } current
                    || current.Id != request.Id
                    || current.Answer is CaseEditRequestAnswer.Withdrawn or CaseEditRequestAnswer.Accepted)
                {
                    return (null, view.Version);
                }
                if (current.Answer == CaseEditRequestAnswer.Declined)
                {
                    throw new CaseEditRequestRefusedException(caseId, view.Version, CaseEditRequestRefusal.Declined);
                }
                if (!view.RequestIsPending)
                {
                    throw new CaseEditRequestRefusedException(caseId, view.Version, CaseEditRequestRefusal.NoAnswer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                await caseLeases.AnswerRequestAsync(
                    new(caseId, actor, request.Id, CaseEditRequestAnswer.Withdrawn, LeaseToken: null),
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // The request was already answered or ended; nothing is left to withdraw.
            }
            throw;
        }
    }

    private static EditLeaseRecordKind ParseKind(string? recordKind) =>
        AutomationMcpErrors.ParseName<EditLeaseRecordKind>(recordKind, "recordKind must be Case, Triage or ImageIntake.");

    private static string Scope(EditLeaseRecordKind kind) =>
        kind == EditLeaseRecordKind.Case ? AutomationMcp.CasesScope : AutomationMcp.IntakeScope;

    /// <summary>The record edit scope a Triage or Image Intake lease is (FRD-14 Record edit scopes).</summary>
    private static EditScopeKind ScopeKind(EditLeaseRecordKind kind) =>
        kind == EditLeaseRecordKind.ImageIntake ? EditScopeKind.ImageIntake : EditScopeKind.Triage;

    private static string RequireToken(string? editLeaseToken) =>
        string.IsNullOrWhiteSpace(editLeaseToken)
            ? throw new McpException("An active edit lease token is required.")
            : editLeaseToken;

    /// <summary>
    /// The Triage Case's Case/PO (decision V), or the Image Intake record's Image Intake
    /// Reference, returned with its lease.
    /// </summary>
    private async Task<string> ScopeReferenceAsync(
        EditLeaseRecordKind kind,
        Guid recordId,
        AutomationActorContext context,
        CancellationToken cancellationToken) =>
        kind == EditLeaseRecordKind.ImageIntake
            ? (await imageIntakes.GetAsync(recordId, cancellationToken)
                ?? throw new McpException("The Image Intake record was not found.")).Record.ImageIntakeReference
            : (await getTriage.ExecuteAsync(new(recordId, context.Actor), cancellationToken)
                ?? throw new McpException("The Triage record was not found.")).Record.Reference;
}
