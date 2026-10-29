using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
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
/// since carries on. Another holder's lease is taken over only when
/// <c>pegasus_edit_begin</c> is given <c>takeOver</c>, and Core decides whether
/// that take-over is allowed: never over another Automation session's lease;
/// nothing here merges another holder's edit. A Case lease needs <c>automation.cases</c>, a Triage lease
/// <c>automation.intake</c>, as the records' own tools do. The lease stands
/// through every write made under it until <c>pegasus_edit_end</c> releases it
/// (operator, 8 October 2026). A single write needs none of this: a write tool
/// given no token holds the lease for that one command
/// (<see cref="AutomationEditLease"/>).
/// </summary>
[McpServerToolType]
internal sealed class EditLeaseMcpTools(
    CaseEditLeases caseLeases,
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
    [Description("Claims the server-owned five-minute edit lease on one Case or Triage Case for multi-step work, using the same guard as staff editing. The lease is issued at the record's current version, which the result returns. The token carries every write made under it: present it on each write until pegasus_edit_end releases it. Fails closed when the expected version is newer than the record's, or when another editor holds the lease unless takeOver is true: then, like a member of staff's Take over, it takes a lease a member of staff holds (the takeover is recorded in the record's history); a lease another Automation session holds is never taken over. A single write needs no lease call: every write tool takes the lease itself for that one command when editLeaseToken is omitted.")]
    public async Task<EditLeaseToolResult> BeginAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The record version the caller observed; a version newer than the record's fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs returns the same lease token while that lease is live.")] string operationKey,
        [Description("Take over a lease a member of staff holds, ending their edit session; never one another Automation session holds. Omit to fail closed while any lease is held.")] bool takeOver = false,
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
                    var lease = await caseLeases.ClaimAsync(
                        new(recordId, expectedVersion, context.Actor, normalizedKey) { TakeOver = takeOver },
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
