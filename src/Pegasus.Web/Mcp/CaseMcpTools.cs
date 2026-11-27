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
    string Origin,
    [property: Description("Whether the Case holds an open query (operator, 11 October 2026); such a Case reads as the Query stage.")] bool HasOpenQuery = false);

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

internal sealed record CaseClaimSourceToolItem(
    [property: Description("The directory Claim source record the Case copies, as pegasus_case_update_details claimSourceId names it.")] Guid? ClaimSourceId,
    string? Name,
    [property: Description("The contact for this Case: the Case's own where one was set, otherwise the record's.")] string? ContactName,
    string? ContactTelephone,
    string? ContactEmail);

internal sealed record CaseRepairerToolItem(
    [property: Description("The directory Repairer the Case is linked to, as pegasus_case_update_details repairerDirectoryId names it; null when the repairer is typed and not linked.")] Guid? DirectoryOrganizationId,
    string? Name,
    string? Address);

internal sealed record CaseHoldsToolItem(
    [property: Description("The Case requirements still unmet, by the names staff see: for example Instructions, Images or the vehicle mileage. Empty when none is.")] IReadOnlyList<string> MissingRequirements,
    [property: Description("While Instructions is unmet, the instruction items the Case's data still lacks, by their field labels.")] IReadOnlyList<string> MissingInstructionItems,
    [property: Description("The Case's document storage failed, which holds it Not ready until pegasus_document_action retry_custody confirms it.")] bool DocumentStorageFailed,
    [property: Description("An Audit's original report is not on the Case yet.")] bool OriginalReportMissing);

internal sealed record CaseGetToolResult(
    CaseSearchToolItem Summary,
    long CaseVersion,
    [property: Description("The Case's Claim source, which the report sending rules read; null when the Case records none.")] CaseClaimSourceToolItem? ClaimSource,
    [property: Description("The Case's Repairer, linked or typed; null when the Case records none.")] CaseRepairerToolItem? Repairer,
    [property: Description("What still holds the Case from being ready, as the Not ready queue and the Case's Next action name it.")] CaseHoldsToolItem Holds,
    CaseEditLeaseToolSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocumentToolItem> Documents,
    string? NextDocumentCursor,
    IReadOnlyList<CaseHistoryToolItem> RecentHistory,
    string? NextHistoryCursor,
    string CorrelationId)
{
    [Description("What a standalone Audit works from: OriginalEngineerReport, or GarageAuthorisationAndInvoice, which waits for the garage's estimate, the Engineer's authorisation of the garage and the repairer's invoice and has no original report. Every other Case type reads OriginalEngineerReport.")]
    public string AuditWorkflow { get; init; } = nameof(AuditWorkflowKind.OriginalEngineerReport);

    [Description("When the garage was marked authorised on a Garage authorisation and invoice Audit (pegasus_case_action authorise_garage); null until then and after undo_garage_authorisation.")]
    public DateTimeOffset? GarageAuthorisedAtUtc { get; init; }
}

/// <summary>
/// Who the connection acts as: every other tool acts as this actor and
/// records its work under it (R1304).
/// </summary>
internal sealed record SessionToolResult(
    [property: Description("Staff for a member of staff signed in from Pegasus Desktop; Automation for the Automation Actor.")] string ActorKind,
    [property: Description("The member of staff's identifier, as the assign actions and pegasus_vocabulary_get name staff; null for the Automation Actor.")] Guid? StaffId,
    [property: Description("The member of staff's Pegasus username; null for the Automation Actor.")] string? UserName,
    [property: Description("The member of staff's role: Administrator, Engineer or User; null for the Automation Actor.")] string? Role,
    [property: Description("The scopes this token holds; tools/list holds only their tools.")] IReadOnlyList<string> Scopes,
    string CorrelationId);

/// <summary>
/// Case reads (MCP-02): thin adapters over the same Core case queries the
/// staff Web UI calls, guarded by the automation.cases scope, with the read
/// of who the connection acts as. Case writes live with the record they
/// change; the explicit edit lease for multi-step work is
/// <see cref="EditLeaseMcpTools"/>.
/// </summary>
[McpServerToolType]
internal sealed class CaseMcpTools(
    SearchCasesByCursor searchCases,
    IGetCaseEditBasis getCaseEditBasis,
    INotReadyCaseQueries notReadyCases,
    IListCaseDocumentsByCursor listDocuments,
    IListCaseHistoryByCursor listHistory,
    Pegasus.Core.Identity.IStaffAccountQueries staffAccounts,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(
        Name = "pegasus_session_get",
        Title = "Get session",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns who this connection acts as: the actor kind, and for a member of staff signed in from Pegasus Desktop their staff identifier, username and role, with the scopes the token holds. Every other tool acts as this actor, with this actor's rights, and records its work under it. It reads nothing else and writes nothing.")]
    public async Task<SessionToolResult> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync("pegasus_session_get", cancellationToken);

        // A read of the connection itself: no record is touched, so only a
        // refusal is history.
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_session_get",
            "session",
            null,
            () => AutomationMcpErrors.ExecuteAsync(context, async () =>
            {
                var actor = context.Actor;
                if (!context.IsStaff || !Guid.TryParse(actor.SubjectId, out var staffId))
                {
                    return new SessionToolResult(
                        actor.Kind.ToString(), null, null, null, context.Scopes, context.TraceIdentifier);
                }

                var names = await staffAccounts.GetNamesAsync([staffId], cancellationToken);
                return new SessionToolResult(
                    actor.Kind.ToString(),
                    staffId,
                    names.GetValueOrDefault(staffId),
                    actor.Roles.Single().ToString(),
                    context.Scopes,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

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
        [Description(AutomationToolText.Cursor)] string? cursor = null,
        [Description(AutomationToolText.PageSize)] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_case_search",
            "case-search",
            async context =>
            {
                CaseLifecycleState? stateFilter = string.IsNullOrWhiteSpace(state)
                    ? null
                    : AutomationMcpErrors.ParseName<CaseLifecycleState>(
                        state,
                        "The lifecycle-state filter is not recognized.");

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
            },
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
    [Description("Returns one case as a bounded projection: its summary and version, its Claim source and Repairer, what still holds it from being ready, and independently paged document inventory and history. Document content is retrieved with pegasus_document_download, or with pegasus_document_download_batch for 1 to 25 documents in one call.")]
    public async Task<CaseGetToolResult> GetAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description("Opaque document cursor returned by the previous response.")] string? documentCursor = null,
        [Description("Opaque history cursor returned by the previous response.")] string? historyCursor = null,
        [Description("Maximum documents and history entries per page, 1 to 100; omit for 50.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_case_get",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                // The frame and data are the Case save's own read; the
                // blockers are the Not ready queue's. Each opens its own
                // context, so they run side by side.
                var basisRead = getCaseEditBasis.ExecuteAsync(
                    new(caseId, context.Actor), cancellationToken);
                var blockersRead = notReadyCases.GetBlockersAsync([caseId], cancellationToken);
                await Task.WhenAll(basisRead, blockersRead);
                var header = await basisRead
                    ?? throw new McpException("The case was not found.");
                var blockers = (await blockersRead).GetValueOrDefault(caseId);
                var data = header.Data;
                var claimSource = data.Workspace?.ClaimSource;
                var repairerName = Accepted(data.Inspection.RepairerName);
                var repairerAddress = Accepted(data.Inspection.RepairerAddress);
                var repairerLink = data.Workspace?.Repairer?.DirectoryOrganizationId;
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
                    claimSource is null
                        ? null
                        : new(
                            claimSource.ClaimSourceId,
                            claimSource.Name,
                            claimSource.EffectiveContactName,
                            claimSource.EffectiveContactTelephone,
                            claimSource.EffectiveContactEmailAddress),
                    repairerLink is null && repairerName is null && repairerAddress is null
                        ? null
                        : new(repairerLink, repairerName, repairerAddress),
                    new(
                        data.Completeness.Evaluation.MissingRequirements,
                        data.Completeness.MissingInstructionItems,
                        blockers?.CustodyFailed ?? false,
                        blockers?.OriginalReportMissing ?? false),
                    header.Frame.ActiveEditLease is { } lease
                        ? new(lease.Holder, lease.ExpiresAtUtc)
                        : null,
                    documents,
                    documentPage.NextCursor,
                    history,
                    historyPage.NextCursor,
                    context.TraceIdentifier)
                {
                    AuditWorkflow = header.Workflow.AuditWorkflow.ToString(),
                    GarageAuthorisedAtUtc = header.Workflow.GarageAuthorisedAtUtc
                };
            },
            cancellationToken);
    }

    private static string? Accepted(CaseField<string>? field) =>
        (field?.Confirmed ?? field?.Fact)?.Value;

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
        item.Origin,
        item.HasOpenQuery);
}
