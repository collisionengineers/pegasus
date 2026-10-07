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
/// same Core ports, so nothing here takes over, forces or merges another
/// holder's edit. A Case lease needs <c>automation.cases</c>, a Triage lease
/// <c>automation.intake</c>, as the records' own tools do. A single write needs
/// none of this: a write tool given no token holds the lease for that one
/// command (<see cref="AutomationEditLease"/>).
/// </summary>
[McpServerToolType]
internal sealed class EditLeaseMcpTools(
    IAcquireCaseEditLease acquireCase,
    IRenewCaseEditLease renewCase,
    IReleaseCaseEditLease releaseCase,
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
    [Description("Claims the server-owned five-minute edit lease on one Case or Triage Case for multi-step work, using the same guard as staff editing. Fails closed when another editor holds the lease or the expected version is stale. A single write needs no lease call: every write tool takes the lease itself for that one command when editLeaseToken is omitted.")]
    public async Task<EditLeaseToolResult> BeginAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The record version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same lease claim.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync(Scope(kind), cancellationToken);
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
                    var lease = await acquireCase.ExecuteAsync(
                        new(recordId, expectedVersion, context.Actor, normalizedKey),
                        cancellationToken);
                    return new EditLeaseToolResult(
                        kind.ToString(), recordId, null, lease.Token, lease.Holder, lease.Version,
                        lease.ExpiresAtUtc, normalizedKey, AutomationMcpAuditor.CorrelationId(context, normalizedKey));
                }

                var scopeLease = await editScopes.ClaimAsync(
                    new(EditScopeKind.Triage, recordId, expectedVersion, context.Actor, normalizedKey),
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
    [Description("Extends the edit lease claimed with pegasus_edit_begin so work that outlasts five minutes continues without re-claiming. A Case renewal needs expectedVersion; a Triage renewal is a heartbeat and ignores it. Fails closed for a non-holder or an expired lease.")]
    public async Task<EditLeaseToolResult> RenewAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The Case version the caller observed; required for a Case, ignored for a Triage.")] long? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync(Scope(kind), cancellationToken);
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
                    var version = expectedVersion
                        ?? throw new McpException("A Case renewal needs the expectedVersion the caller observed.");
                    var lease = await renewCase.ExecuteAsync(
                        new(recordId, version, context.Actor, normalizedKey, token),
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
    [Description("Releases an edit lease claimed with pegasus_edit_begin, so the record is free for colleagues at once.")]
    public async Task<EditLeaseReleaseToolResult> EndAsync(
        [Description(RecordKindDescription)] string recordKind,
        [Description("The Case identifier, or the Triage Case identifier.")] Guid recordId,
        [Description("The lease token returned by pegasus_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var kind = ParseKind(recordKind);
        var context = await resolver.RequireAsync(Scope(kind), cancellationToken);
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
                if (kind == EditLeaseRecordKind.Case)
                {
                    await releaseCase.ExecuteAsync(
                        new(recordId, context.Actor, normalizedKey, token),
                        cancellationToken);
                }
                else
                {
                    await editScopes.ReleaseAsync(
                        new(EditScopeKind.Triage, recordId, context.Actor, normalizedKey, token),
                        cancellationToken);
                }
                return new EditLeaseReleaseToolResult(
                    kind.ToString(), recordId, Released: true, normalizedKey,
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
