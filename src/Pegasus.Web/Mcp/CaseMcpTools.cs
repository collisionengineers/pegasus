using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record CaseMcpWorkflowSummary(
    Guid CaseId,
    CaseIdentity Identity,
    CaseLifecycleState State,
    Guid? AssignedEngineerId,
    Guid? ReportApprovalId,
    Guid? ReportSentEvidenceId,
    CaseDueWork? DueWork,
    CaseClosureOutcome? ClosureOutcome,
    Guid? OriginalCaseId,
    Guid? ReplacementCaseId,
    long Version,
    CaseArchive? Archive)
{
    public static CaseMcpWorkflowSummary From(CaseWorkflowRecord workflow) =>
        new(
            workflow.CaseId,
            workflow.Identity,
            workflow.State,
            workflow.AssignedEngineerId,
            workflow.ReportApproval?.ApprovalId,
            workflow.ReportSentEvidence?.EvidenceId,
            workflow.DueWork,
            workflow.ClosureOutcome,
            workflow.OriginalCaseId,
            workflow.ReplacementCaseId,
            workflow.Version,
            workflow.Archive);
}

internal sealed record CaseMcpDocumentOccurrence(
    Guid Id,
    Guid DocumentId,
    Guid VersionId,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    DateTimeOffset RecordedAtUtc);

internal sealed record CaseMcpDocumentVersion(
    Guid Id,
    Guid DocumentId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentCustodyStatus CustodyStatus,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    bool IsCurrent,
    bool IsLogicallyRemoved,
    string? RemovalReason);

internal sealed record CaseMcpDocument(
    Guid Id,
    Guid CaseId,
    IReadOnlyList<CaseMcpDocumentOccurrence> Occurrences,
    IReadOnlyList<CaseMcpDocumentVersion> Versions)
{
    public static CaseMcpDocument From(CaseDocument document) =>
        new(
            document.Id,
            document.CaseId,
            [.. document.Occurrences.Take(100).Select(occurrence =>
                new CaseMcpDocumentOccurrence(
                    occurrence.Id,
                    occurrence.DocumentId,
                    occurrence.VersionId,
                    occurrence.SemanticRole,
                    occurrence.Source,
                    occurrence.RecordedAtUtc))],
            [.. document.Versions.Take(100).Select(version => new CaseMcpDocumentVersion(
                version.Id,
                version.DocumentId,
                version.Version,
                DocumentMcpContent.SanitizeFileName(version.FileName),
                version.MediaType,
                version.ContentLength,
                version.Sha256,
                version.CustodyStatus,
                version.CreatedAtUtc,
                version.CreatedBy,
                version.IsCurrent,
                version.IsLogicallyRemoved,
                version.RemovalReason))]);
}

internal sealed record CaseMcpReportEvidenceCandidate(
    Guid EvidenceId,
    string SourceSha256,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc);

internal sealed record CaseMcpEvaImage(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentSource Source);

internal sealed record CaseMcpEvaPreparation(
    Guid CaseId,
    long CaseVersion,
    string Reference,
    IReadOnlyList<CaseMcpEvaImage> Images,
    IReadOnlyList<EvaHandoffRevisionSummary> Revisions,
    DateTimeOffset? FirstSentToEngineerAtUtc,
    IReadOnlyList<string> BlockingReasons)
{
    public static CaseMcpEvaPreparation From(EvaHandoffPreparation preparation) =>
        new(
            preparation.CaseId,
            preparation.CaseVersion,
            preparation.Reference,
            [.. preparation.Images.Take(100).Select(image => new CaseMcpEvaImage(
                image.OccurrenceId,
                image.DocumentId,
                image.VersionId,
                image.Version,
                DocumentMcpContent.SanitizeFileName(image.FileName),
                image.MediaType,
                image.ContentLength,
                image.Sha256,
                image.Source))],
            [.. preparation.Revisions.Take(100)],
            preparation.FirstSentToEngineerAtUtc,
            [.. preparation.BlockingReasons.Take(100)]);
}

internal sealed record CaseMcpDetail(
    CaseSearchItem Summary,
    CaseMcpWorkflowSummary Workflow,
    CaseDataProjection? Data,
    CaseEditLeaseSnapshot? ActiveEditLease,
    IReadOnlyList<CaseMcpDocument> Documents,
    IReadOnlyList<BoxFileRequest> BoxFileRequests,
    IReadOnlyList<CaseRequestUploadSummary> RequestUploadLinks,
    IReadOnlyList<CaseMcpReportEvidenceCandidate> AvailableReportSentEvidence,
    IReadOnlyList<CaseHistoryEntry> History,
    IReadOnlyList<CaseTaskRecord> Tasks,
    CaseVehicleEvidence? VehicleEvidence,
    CaseMcpEvaPreparation? EvaHandoff,
    bool IsTruncated)
{
    private const int MaximumCollectionItems = 100;

    public static CaseMcpDetail From(CaseDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new(
            details.Summary,
            CaseMcpWorkflowSummary.From(details.Workflow),
            details.Data,
            details.ActiveEditLease,
            [.. details.Documents.Take(MaximumCollectionItems).Select(CaseMcpDocument.From)],
            [.. details.BoxFileRequests.Take(MaximumCollectionItems)],
            [.. details.RequestUploadLinks.Take(MaximumCollectionItems)],
            [.. details.AvailableReportSentEvidence
                .Take(MaximumCollectionItems)
                .Select(evidence => new CaseMcpReportEvidenceCandidate(
                    evidence.EvidenceId,
                    evidence.SourceSha256,
                    evidence.MimeSha256,
                    evidence.SentAtUtc,
                    evidence.DiscoveredAtUtc))],
            [.. details.History.Take(MaximumCollectionItems)],
            [.. details.Tasks.Take(MaximumCollectionItems)],
            details.VehicleEvidence,
            details.EvaHandoff is { } eva
                ? CaseMcpEvaPreparation.From(eva)
                : null,
            details.Documents.Count > MaximumCollectionItems
                || details.BoxFileRequests.Count > MaximumCollectionItems
                || details.RequestUploadLinks.Count > MaximumCollectionItems
                || details.AvailableReportSentEvidence.Count > MaximumCollectionItems
                || details.History.Count > MaximumCollectionItems
                || details.Tasks.Count > MaximumCollectionItems);
    }
}

[McpServerToolType]
internal sealed class CasesSearchMcpTool(
    ISearchCases searchCases,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesSearch,
        Title = "Search cases",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Searches one bounded page of cases authorized for the current staff member.")]
    public Task<StaffMcpResult<SearchCasesResult>> ExecuteAsync(
        CaseSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(filters);
            StaffMcpInput.RequirePage(page, pageSize);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await searchCases.ExecuteAsync(
                new(staff.Actor, filters, page, pageSize),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesGetMcpTool(
    IGetCase getCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesGet,
        Title = "Get case",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets bounded case data, workflow and evidence metadata without custody coordinates.")]
    public async Task<StaffMcpResult<CaseMcpDetail>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
        var result = await StaffMcpCall.ExecuteAsync(async () =>
        {
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await getCase.ExecuteAsync(
                new(caseId, staff.Actor),
                cancellationToken);
        });
        return result.Outcome == StaffMcpCallOutcome.Succeeded
            ? result.Value is { } details
                ? StaffMcpResult<CaseMcpDetail>.Succeeded(CaseMcpDetail.From(details))
                : StaffMcpResult<CaseMcpDetail>.NotFound()
            : new(result.Outcome, null, result.ErrorCode, result.CurrentVersion);
    }
}

[McpServerToolType]
internal sealed class CasesSaveMcpTool(
    ISaveCase saveCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesSave,
        Title = "Save case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Saves the explicit editable case fields using the current version and active edit lease.")]
    public Task<StaffMcpResult<CaseDataProjection>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CaseEditableData data,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            ArgumentNullException.ThrowIfNull(data);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await saveCase.ExecuteAsync(
                new(caseId, expectedVersion, staff.Actor, operationKey, reason, editLeaseToken, data),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesConfirmCompletenessMcpTool(
    IConfirmCompleteness confirmCompleteness,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesConfirmCompleteness,
        Title = "Confirm case completeness",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Confirms case completeness through the configured Core policy using the active edit lease.")]
    public Task<StaffMcpResult<CaseDataProjection>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CaseCompleteness completeness,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            ArgumentNullException.ThrowIfNull(completeness);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await confirmCompleteness.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    completeness),
                cancellationToken);
        });
}
