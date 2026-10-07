using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
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
    string Origin);

internal sealed record CaseSearchToolResult(
    IReadOnlyList<CaseSearchToolItem> Items,
    string? NextCursor,
    int Limit,
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
    string? Reason,
    long BeforeVersion,
    long AfterVersion);

internal sealed record CaseGetToolResult(
    CaseSearchToolItem Summary,
    long CaseVersion,
    Guid? AssignedEngineerId,
    CaseEditLeaseToolSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocumentToolItem> Documents,
    string? NextDocumentCursor,
    IReadOnlyList<CaseHistoryToolItem> RecentHistory,
    string? NextHistoryCursor,
    string CorrelationId);

/// <summary>
/// Automation Actor Case reads (MCP-02): thin adapters over the same Core
/// case queries the staff Web UI calls, guarded by the automation.cases
/// scope. Case writes live with the record they change; the explicit edit
/// lease for multi-step work is <see cref="EditLeaseMcpTools"/>.
/// </summary>
[McpServerToolType]
internal sealed class CaseMcpTools(
    ISearchCasesByCursor searchCases,
    IGetCaseHeader getCaseHeader,
    IListCaseDocumentsByCursor listDocuments,
    IListCaseHistoryByCursor listHistory,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(
        Name = "pegasus_case_search",
        Title = "Search cases",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Searches cases by free text, reference, registration, claimant, claim number, principal, and lifecycle state. Returns a bounded page and an opaque continuation cursor.")]
    public async Task<CaseSearchToolResult> SearchAsync(
        [Description("Free-text query over reference, registration, claimant, and claim number.")] string? query = null,
        [Description("Exact or partial case reference filter.")] string? caseReference = null,
        [Description("Vehicle registration filter.")] string? registration = null,
        [Description("Claimant name filter.")] string? claimant = null,
        [Description("Claim number filter.")] string? claimNumber = null,
        [Description("Principal code filter.")] string? principal = null,
        [Description("Lifecycle state filter using the CaseLifecycleState name.")] string? state = null,
        [Description("Opaque cursor returned by the previous page; omit for the first page.")] string? cursor = null,
        [Description("Page size between 1 and 100; omit for 50.")] int? limit = null,
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

                var effectiveLimit = CursorPaging.NormalizeLimit(limit);

                var result = await searchCases.ExecuteAsync(
                    new(
                        context.Actor,
                        new CaseSearchFilters(
                            CaseReference: caseReference,
                            Registration: registration,
                            Claimant: claimant,
                            ClaimNumber: claimNumber,
                            Principal: principal,
                            States: stateFilter is { } stateValue ? [stateValue] : null,
                            Query: query),
                        Cursor: cursor,
                        Limit: effectiveLimit),
                    cancellationToken);
                return new CaseSearchToolResult(
                    result.Items.Select(Map).ToArray(),
                    result.NextCursor,
                    effectiveLimit,
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
    [Description("Returns one case as a bounded projection with independently paged document inventory and history. Document content is retrieved with pegasus_document_download.")]
    public async Task<CaseGetToolResult> GetAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("Opaque document cursor returned by the previous response.")] string? documentCursor = null,
        [Description("Opaque history cursor returned by the previous response.")] string? historyCursor = null,
        [Description("Maximum documents and history entries per page, 1 to 100; omit for 50.")] int? limit = null,
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
                var header = await getCaseHeader.ExecuteAsync(
                    new(caseId, context.Actor),
                    cancellationToken)
                    ?? throw new McpException("The case was not found.");
                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var documentPage = await listDocuments.ExecuteAsync(
                    new(context.Actor, caseId, documentCursor, effectiveLimit), cancellationToken);
                var historyPage = await listHistory.ExecuteAsync(
                    new(context.Actor, caseId, historyCursor, effectiveLimit), cancellationToken);
                var documents = documentPage.Items
                    .Select(document =>
                    {
                        var occurrence = document.Occurrence;
                        var version = document.Version;
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
                    })
                    .ToArray();
                var history = historyPage.Items
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
                    Map(header.Summary),
                    header.Workflow.Version,
                    header.Workflow.AssignedEngineerId,
                    header.ActiveEditLease is { } lease
                        ? new(lease.Holder, lease.ExpiresAtUtc)
                        : null,
                    documents,
                    documentPage.NextCursor,
                    history,
                    historyPage.NextCursor,
                    context.TraceIdentifier);
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
        item.Origin);
}
