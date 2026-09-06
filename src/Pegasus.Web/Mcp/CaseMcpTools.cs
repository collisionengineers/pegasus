using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record CaseSearchToolItem(
    Guid CaseId,
    string Reference,
    string? AuditReference,
    string CaseType,
    string Principal,
    string State,
    Guid? EngineerId,
    string? Registration,
    string? Claimant,
    string? ClaimNumber,
    DateTimeOffset ReceivedAtUtc,
    DateOnly? InstructionDate,
    string Origin);

internal sealed record CaseSearchToolResult(
    IReadOnlyList<CaseSearchToolItem> Items,
    int Page,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage,
    string CorrelationId);

internal sealed record CaseEditLeaseToolSnapshot(
    string Holder,
    DateTimeOffset ExpiresAtUtc);

internal sealed record CaseDocumentToolItem(
    Guid OccurrenceId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string SemanticRole,
    string Source,
    DateTimeOffset RecordedAtUtc,
    bool IsCurrent,
    bool IsLogicallyRemoved);

internal sealed record CaseHistoryToolItem(
    string EventType,
    string Actor,
    string ActorKind,
    DateTimeOffset OccurredAtUtc,
    string Reason,
    long BeforeVersion,
    long AfterVersion);

internal sealed record CaseGetToolResult(
    CaseSearchToolItem Summary,
    long CaseVersion,
    Guid? AssignedEngineerId,
    CaseEditLeaseToolSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocumentToolItem> Documents,
    int DocumentEntryCount,
    IReadOnlyList<CaseHistoryToolItem> RecentHistory,
    int HistoryEntryCount,
    string CorrelationId);

internal sealed record CaseEditLeaseToolResult(
    Guid CaseId,
    string LeaseToken,
    string Holder,
    long CaseVersion,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey,
    string CorrelationId);

internal sealed record CaseEditReleaseToolResult(
    Guid CaseId,
    bool Released,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor Case tools (MCP-02): thin adapters over the same Core
/// case use cases the staff Web UI calls, guarded by the automation.cases
/// scope. Case mutations elsewhere present the same edit lease and expected
/// version as a staff save, which is why lease acquire/renew/release are
/// tools: the Automation Actor gets the continuity staff already have, and
/// nothing here takes over, forces, or merges another holder's edit.
/// </summary>
[McpServerToolType]
internal sealed class CaseMcpTools(
    ISearchCases searchCases,
    IGetCase getCase,
    IAcquireCaseEditLease acquireLease,
    IRenewCaseEditLease renewLease,
    IReleaseCaseEditLease releaseLease,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const int MaximumSearchPageSize = 100;
    private const int MaximumDocumentEntries = 200;
    private const int MaximumHistoryEntries = 20;

    [McpServerTool(
        Name = "pegasus_case_search",
        Title = "Search cases",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Searches cases by free text, reference, registration, claimant, claim number, principal, and lifecycle state. Results are paginated; page size defaults to 50 and is capped at 100.")]
    public async Task<CaseSearchToolResult> SearchAsync(
        [Description("Free-text query over reference, registration, claimant, and claim number.")] string? query = null,
        [Description("Exact or partial case reference filter.")] string? caseReference = null,
        [Description("Vehicle registration filter.")] string? registration = null,
        [Description("Claimant name filter.")] string? claimant = null,
        [Description("Claim number filter.")] string? claimNumber = null,
        [Description("Principal code filter.")] string? principal = null,
        [Description("Lifecycle state filter using the CaseLifecycleState name.")] string? state = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size between 1 and 100; 0 selects the default of 50.")] int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_search",
            "case-search",
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                CaseLifecycleState? stateFilter = null;
                if (!string.IsNullOrWhiteSpace(state))
                {
                    if (!Enum.TryParse<CaseLifecycleState>(state.Trim(), ignoreCase: true, out var parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        throw new McpException("The lifecycle-state filter is not recognized.");
                    }

                    stateFilter = parsed;
                }

                var effectivePage = page == 0 ? 1 : page;
                var effectivePageSize = pageSize == 0 ? 50 : pageSize;
                if (effectivePageSize is < 1 or > MaximumSearchPageSize)
                {
                    throw new McpException(
                        $"The page size must be between 1 and {MaximumSearchPageSize}.");
                }

                var result = await searchCases.ExecuteAsync(
                    new(
                        context.Actor,
                        new CaseSearchFilters(
                            CaseReference: caseReference,
                            Registration: registration,
                            Claimant: claimant,
                            ClaimNumber: claimNumber,
                            Principal: principal,
                            State: stateFilter,
                            Query: query),
                        effectivePage,
                        effectivePageSize),
                    cancellationToken);
                return new CaseSearchToolResult(
                    result.Items.Select(Map).ToArray(),
                    result.Page,
                    result.PageSize,
                    result.HasPreviousPage,
                    result.HasNextPage,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_get",
        Title = "Get case",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns one case as a bounded projection: summary, current version, active edit lease, document inventory (capped at 200 entries), and the most recent history entries. Document content is retrieved with pegasus_document_download.")]
    public async Task<CaseGetToolResult> GetAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_get",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var details = await getCase.ExecuteAsync(
                    new(caseId, context.Actor),
                    cancellationToken)
                    ?? throw new McpException("The case was not found.");
                var documents = details.Documents
                    .SelectMany(document => document.Occurrences.Select(occurrence =>
                    {
                        var version = document.Versions.Single(
                            value => value.Id == occurrence.VersionId);
                        return new CaseDocumentToolItem(
                            occurrence.Id,
                            version.Id,
                            version.FileName,
                            version.MediaType,
                            version.ContentLength,
                            occurrence.SemanticRole.ToString(),
                            occurrence.Source.ToString(),
                            occurrence.RecordedAtUtc,
                            version.IsCurrent,
                            version.IsLogicallyRemoved);
                    }))
                    .ToArray();
                var history = details.History
                    .Select(entry => new CaseHistoryToolItem(
                        entry.EventType,
                        entry.Actor,
                        entry.ActorKind,
                        entry.OccurredAtUtc,
                        entry.Reason,
                        entry.BeforeVersion,
                        entry.AfterVersion))
                    .ToArray();
                return new CaseGetToolResult(
                    Map(details.Summary),
                    details.Workflow.Version,
                    details.Workflow.AssignedEngineerId,
                    details.ActiveEditLease is { } lease
                        ? new(lease.Holder, lease.ExpiresAtUtc)
                        : null,
                    documents.Take(MaximumDocumentEntries).ToArray(),
                    documents.Length,
                    history.Take(MaximumHistoryEntries).ToArray(),
                    history.Length,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_edit_begin",
        Title = "Begin case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Claims the server-owned short-lived case edit lease that every case mutation must present, using the same guard as staff editing. Fails closed when another editor holds the lease or the expected version is stale.")]
    public async Task<CaseEditLeaseToolResult> EditBeginAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same lease claim.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_edit_begin",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var lease = await acquireLease.ExecuteAsync(
                    new(caseId, expectedVersion, context.Actor, normalizedKey),
                    cancellationToken);
                return new CaseEditLeaseToolResult(
                    lease.CaseId,
                    lease.Token,
                    lease.Holder,
                    lease.Version,
                    lease.ExpiresAtUtc,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_edit_renew",
        Title = "Renew case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Extends the case edit lease claimed with pegasus_case_edit_begin, using the same Core use case as the staff renew control, so automation whose work outlasts the lease continues without re-claiming. Fails closed for a non-holder, an expired lease, or a stale expected version.")]
    public async Task<CaseEditLeaseToolResult> EditRenewAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("The lease token returned by pegasus_case_edit_begin.")] string leaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same renewal.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);

        // Routine renewal is telemetry, not permanent history; only the refusal is material.
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_case_edit_renew",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (string.IsNullOrWhiteSpace(leaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }

                var lease = await renewLease.ExecuteAsync(
                    new(caseId, expectedVersion, context.Actor, normalizedKey, leaseToken),
                    cancellationToken);
                return new CaseEditLeaseToolResult(
                    lease.CaseId,
                    lease.Token,
                    lease.Holder,
                    lease.Version,
                    lease.ExpiresAtUtc,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_edit_end",
        Title = "End case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Releases a case edit lease previously claimed with pegasus_case_edit_begin.")]
    public async Task<CaseEditReleaseToolResult> EditEndAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The lease token returned by pegasus_case_edit_begin.")] string leaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_edit_end",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (string.IsNullOrWhiteSpace(leaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }

                await releaseLease.ExecuteAsync(
                    new(caseId, context.Actor, normalizedKey, leaseToken),
                    cancellationToken);
                return new CaseEditReleaseToolResult(
                    caseId,
                    Released: true,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    private static CaseSearchToolItem Map(CaseSearchItem item) => new(
        item.CaseId,
        item.Reference,
        item.AuditReference,
        item.CaseType.ToString(),
        item.Principal,
        item.State.ToString(),
        item.EngineerId,
        item.Registration,
        item.Claimant,
        item.ClaimNumber,
        item.ReceivedAtUtc,
        item.InstructionDate,
        item.Origin);
}
