using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal enum EditLeaseRecordKind
{
    Case,
    Triage
}

internal sealed record EditLeaseToolResult(
    string RecordKind,
    Guid RecordId,
    string? Reference,
    string EditLeaseToken,
    string Holder,
    long Version,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey,
    string CorrelationId);

internal sealed record EditLeaseReleaseToolResult(
    string RecordKind,
    Guid RecordId,
    bool Released,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// The explicit edit lease for multi-step automation work on a Case or a
/// Triage Case: the same server-owned lease staff editing claims, through the
/// same Core ports and the same rules for both kinds (R515 to R519): a claim is
/// made at the record's current version and refused only for a newer one, a
/// replayed claim returns the same token, and a lapsed lease nobody claimed
/// since carries on. With <c>takeOver</c> on a Case, <c>pegasus_edit_begin</c>
/// asks the member of staff holding the lease to hand it over and waits inside
/// the call for the answer (R554, 30 October 2026); the Automation Actor never
/// forces a hand-over. With <c>takeOver</c> on a Triage Case it takes over a
/// staff scope at once (R556). Never over another Automation session's lease;
/// nothing here merges another holder's edit. A Case lease needs <c>automation.cases</c>, a Triage lease
/// <c>automation.intake</c>, as the records' own tools do. The lease stands
/// through every write made under it until <c>pegasus_edit_end</c> releases it
/// (operator, 8 October 2026). A single write needs none of this: a write tool
/// given no token holds the lease for that one command
/// (<see cref="Pegasus.Web.Presentation.OneCommandEdit"/>).
/// </summary>
[McpServerToolType]
internal sealed class EditLeaseMcpTools(
    CaseEditLeases caseLeases,
    CaseEditRequestWatch requestWatch,
    IEditScopeLeases editScopes,
    IGetTriage getTriage,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const string RecordKindDescription = "Case or Triage: which record's lease.";

    [McpServerTool(
        Name = "pegasus_edit_begin",
        Title = "Begin edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Claims the server-owned five-minute edit lease on one Case or Triage Case for multi-step work, using the same guard as staff editing. The lease is issued at the record's current version, which the result returns. The token carries every write made under it: present it on each write until pegasus_edit_end releases it. Fails closed when the expected version is newer than the record's, or when another editor holds the lease. With takeOver on a Case, the member of staff holding the lease is asked to hand it over and the call waits up to 45 seconds for their answer, returning the lease if they accept; it never forces a hand-over, and a lease another Automation session holds is never taken. With takeOver on a Triage Case it takes over a staff scope at once, as before (the takeover is recorded in the record's history). A single write needs no lease call: every write tool takes the lease itself for that one command when editLeaseToken is omitted.")]
    public async Task<EditLeaseToolResult> BeginAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The record version the caller observed; a version newer than the record's fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs returns the same lease token while that lease is live.")] string operationKey,
        [Description("On a Case: ask the member of staff holding the lease to hand it over and wait up to 45 seconds for their answer; never a force. On a Triage Case: take over a staff scope at once. Never one another Automation session holds. Omit to fail closed while any lease is held.")] bool takeOver = false,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_begin", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_edit_begin",
            Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(recordId, "record identifier");
                if (kind == EditLeaseRecordKind.Case)
                {
                    var lease = takeOver
                        ? await AskForCaseLeaseAsync(recordId, expectedVersion, context.Actor, normalizedKey, cancellationToken)
                        : await caseLeases.ClaimAsync(
                            new(recordId, expectedVersion, context.Actor, normalizedKey),
                            cancellationToken);
                    return new EditLeaseToolResult(
                        kind.ToString(), recordId, null, lease.Token, lease.Holder, lease.Version,
                        lease.ExpiresAtUtc, normalizedKey, AutomationMcpAuditor.CorrelationId(context, normalizedKey));
                }

                var scopeLease = await editScopes.ClaimAsync(
                    new(EditScopeKind.Triage, recordId, expectedVersion, context.Actor, normalizedKey)
                    {
                        TakeOver = takeOver
                    },
                    cancellationToken);
                return new EditLeaseToolResult(
                    kind.ToString(), recordId, await TriageReferenceAsync(recordId, context, cancellationToken),
                    scopeLease.Token, scopeLease.Holder, scopeLease.RecordVersion, scopeLease.ExpiresAtUtc,
                    normalizedKey, AutomationMcpAuditor.CorrelationId(context, normalizedKey));
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
    [Description("Extends the edit lease claimed with pegasus_edit_begin so work that outlasts five minutes continues without re-claiming. A renewal is a heartbeat, for a Case as for a Triage, and takes no version; it returns the same token. A lease that lapsed with nobody claiming the record since carries on. Fails closed for a non-holder, or once another editor has claimed the record.")]
    public async Task<EditLeaseToolResult> RenewAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_renew", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);

        // Routine renewal is telemetry, not permanent history; only the refusal is material.
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_edit_renew",
            Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
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
                        lease.ExpiresAtUtc, normalizedKey, AutomationMcpAuditor.CorrelationId(context, normalizedKey));
                }

                var scopeLease = await editScopes.HeartbeatAsync(
                    new(EditScopeKind.Triage, recordId, context.Actor, token),
                    cancellationToken);
                return new EditLeaseToolResult(
                    kind.ToString(), recordId, await TriageReferenceAsync(recordId, context, cancellationToken),
                    scopeLease.Token, scopeLease.Holder, scopeLease.RecordVersion, scopeLease.ExpiresAtUtc,
                    normalizedKey, AutomationMcpAuditor.CorrelationId(context, normalizedKey));
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
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync("pegasus_edit_end", Scope(kind), cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_edit_end",
            Resource(recordId),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
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
                            new(EditScopeKind.Triage, recordId, context.Actor, normalizedKey, token),
                            cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is CaseEditLeaseExpiredException or EditScopeExpiredException)
                {
                    released = false;
                }
                return new EditLeaseReleaseToolResult(
                    kind.ToString(), recordId, released, normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
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
            if (raised.Outcome == CaseEditRequestOutcome.Accepted && raised.Lease is { } handed)
            {
                return handed;
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
        Enum.TryParse<EditLeaseRecordKind>(recordKind?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("recordKind must be Case or Triage.");

    private static string Scope(EditLeaseRecordKind kind) =>
        kind == EditLeaseRecordKind.Case ? AutomationMcp.CasesScope : AutomationMcp.IntakeScope;

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    private static string RequireToken(string? editLeaseToken) =>
        string.IsNullOrWhiteSpace(editLeaseToken)
            ? throw new McpException("An active edit lease token is required.")
            : editLeaseToken;

    /// <summary>The Triage Case's Case/PO, returned with its lease (decision V).</summary>
    private async Task<string> TriageReferenceAsync(
        Guid triageId,
        AutomationActorContext context,
        CancellationToken cancellationToken) =>
        (await getTriage.ExecuteAsync(new(triageId, context.Actor), cancellationToken)
            ?? throw new McpException("The Triage record was not found.")).Record.Reference;
}
