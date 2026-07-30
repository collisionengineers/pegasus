using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Mcp;

internal sealed record TriageMcpRecord(
    Guid Id,
    Guid ReceiptId,
    Guid EvaluationRevisionId,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedCaseId,
    long Version)
{
    public static TriageMcpRecord From(TriageRecord record) =>
        new(
            record.Id,
            record.Origin.ReceiptId,
            record.Origin.EvaluationRevisionId,
            record.NormalizedVehicleRegistration,
            record.State,
            record.AssigneeId,
            record.LinkedCaseId,
            record.Version);
}

internal sealed record TriageMcpResponseEvidenceCandidate(
    Guid PollOutcomeId,
    Guid SentEvidenceId,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc);

internal sealed record TriageMcpDetail(
    TriageMcpRecord Record,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TriageFinding> Findings,
    IReadOnlyList<TriageResponseEvidenceLink> ResponseEvidence,
    IReadOnlyList<TriageHistoryEntry> History,
    IReadOnlyList<TriageMcpResponseEvidenceCandidate> ResponseEvidenceCandidates)
{
    private const int MaximumRelatedItems = 100;

    public static TriageMcpDetail From(TriageDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new(
            TriageMcpRecord.From(detail.Record),
            detail.CreatedAtUtc,
            [.. detail.Findings.Take(MaximumRelatedItems)],
            [.. detail.ResponseEvidence.Take(MaximumRelatedItems)],
            [.. detail.History.Take(MaximumRelatedItems)],
            [.. detail.ResponseEvidenceCandidates
                .Take(MaximumRelatedItems)
                .Select(candidate => new TriageMcpResponseEvidenceCandidate(
                    candidate.PollOutcomeId,
                    candidate.SentEvidenceId,
                    candidate.SentAtUtc,
                    candidate.DiscoveredAtUtc))]);
    }
}

internal static class TriageMcpInput
{
    public static void ValidateMutation(
        Guid triageId,
        long expectedVersion,
        ref string operationKey,
        ref string reason)
    {
        StaffMcpInput.RequireIdentifier(triageId, nameof(triageId));
        StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
        operationKey = StaffMcpInput.RequireOperationKey(operationKey);
        reason = StaffMcpInput.RequireReason(reason);
    }
}

[McpServerToolType]
internal sealed class TriageListMcpTool(
    IListTriage listTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageList,
        Title = "List Triage work",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists a bounded page of durable Triage records authorized for the current staff actor.")]
    public Task<StaffMcpResult<TriageListPage>> ExecuteAsync(
        TriageState? state,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequirePage(page, pageSize);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await listTriage.ExecuteAsync(
                new(staff.Actor, state, page, pageSize),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class TriageGetMcpTool(
    IGetTriage getTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageGet,
        Title = "Get Triage detail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets bounded Triage detail plus exact response-evidence candidates without mailbox custody coordinates.")]
    public async Task<StaffMcpResult<TriageMcpDetail>> ExecuteAsync(
        Guid triageId,
        CancellationToken cancellationToken)
    {
        StaffMcpInput.RequireIdentifier(triageId, nameof(triageId));
        var result = await StaffMcpCall.ExecuteAsync(async () =>
        {
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await getTriage.ExecuteAsync(
                new(triageId, staff.Actor),
                cancellationToken);
        });
        return result.Outcome == StaffMcpCallOutcome.Succeeded
            ? result.Value is { } detail
                ? StaffMcpResult<TriageMcpDetail>.Succeeded(TriageMcpDetail.From(detail))
                : StaffMcpResult<TriageMcpDetail>.NotFound()
            : new(result.Outcome, null, result.ErrorCode, result.CurrentVersion);
    }
}

[McpServerToolType]
internal sealed class TriageAssignMcpTool(
    IAssignTriage assignTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageAssign,
        Title = "Assign Triage",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Assigns a current Triage record with exact optimistic concurrency and idempotency preconditions.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        Guid assigneeId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedVersion,
                ref operationKey,
                ref reason);
            StaffMcpInput.RequireIdentifier(assigneeId, nameof(assigneeId));
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return TriageMcpRecord.From(await assignTriage.ExecuteAsync(
                new(
                    triageId,
                    expectedVersion,
                    assigneeId,
                    staff.HistoryActor,
                    operationKey,
                    reason),
                cancellationToken));
        });
}

[McpServerToolType]
internal sealed class TriageUnassignMcpTool(
    IUnassignTriage unassignTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageUnassign,
        Title = "Unassign Triage",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Removes the current Triage assignment with exact version and idempotency preconditions.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            unassignTriage.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            cancellationToken);

    internal static Task<StaffMcpResult<TriageMcpRecord>> ExecuteMutationAsync(
        Func<TriageMutationRequest, CancellationToken, Task<TriageRecord>> execute,
        StaffMcpActorResolver actorResolver,
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedVersion,
                ref operationKey,
                ref reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return TriageMcpRecord.From(await execute(
                new(
                    triageId,
                    expectedVersion,
                    staff.HistoryActor,
                    operationKey,
                    reason),
                cancellationToken));
        });
}

[McpServerToolType]
internal sealed class TriageRecordFindingMcpTool(
    IRecordTriageFinding recordFinding,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageRecordFinding,
        Title = "Record Triage finding",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records one roadworthiness or assessment finding against the current Triage version.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        CancellationToken cancellationToken) =>
        ExecuteFindingAsync(
            recordFinding.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            roadworthiness,
            assessment,
            supersedesFindingId: null,
            cancellationToken);

    internal static Task<StaffMcpResult<TriageMcpRecord>> ExecuteFindingAsync(
        Func<RecordTriageFindingRequest, CancellationToken, Task<TriageRecord>> execute,
        StaffMcpActorResolver actorResolver,
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        Guid? supersedesFindingId,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedVersion,
                ref operationKey,
                ref reason);
            if (roadworthiness is null && assessment is null)
            {
                throw new ModelContextProtocol.McpException(
                    "At least one finding value is required.");
            }
            if (supersedesFindingId == Guid.Empty)
            {
                throw new ModelContextProtocol.McpException(
                    "'supersedesFindingId' must be a non-empty identifier when supplied.");
            }
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return TriageMcpRecord.From(await execute(
                new(
                    triageId,
                    expectedVersion,
                    staff.HistoryActor,
                    operationKey,
                    reason,
                    roadworthiness,
                    assessment,
                    supersedesFindingId),
                cancellationToken));
        });
}

[McpServerToolType]
internal sealed class TriageSupersedeFindingMcpTool(
    ISupersedeTriageFinding supersedeFinding,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageSupersedeFinding,
        Title = "Supersede Triage finding",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records the corrected successor of an exact prior Triage finding.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        Guid supersedesFindingId,
        string operationKey,
        string reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        CancellationToken cancellationToken)
    {
        StaffMcpInput.RequireIdentifier(supersedesFindingId, nameof(supersedesFindingId));
        return TriageRecordFindingMcpTool.ExecuteFindingAsync(
            supersedeFinding.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            roadworthiness,
            assessment,
            supersedesFindingId,
            cancellationToken);
    }
}

[McpServerToolType]
internal sealed class TriageLinkResponseMcpTool(
    ILinkTriageResponseEvidence linkResponse,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageLinkResponse,
        Title = "Link Triage response evidence",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Links one exact sent-response candidate after Core revalidates its poll outcome and reply chain.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        Guid pollOutcomeId,
        Guid sentEvidenceId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedVersion,
                ref operationKey,
                ref reason);
            StaffMcpInput.RequireIdentifier(pollOutcomeId, nameof(pollOutcomeId));
            StaffMcpInput.RequireIdentifier(sentEvidenceId, nameof(sentEvidenceId));
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await linkResponse.ExecuteAsync(
                new(
                    triageId,
                    pollOutcomeId,
                    sentEvidenceId,
                    expectedVersion,
                    staff.HistoryActor,
                    operationKey,
                    reason),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class TriageUnlinkResponseMcpTool(
    IUnlinkTriageResponseEvidence unlinkResponse,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageUnlinkResponse,
        Title = "Unlink Triage response evidence",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Unlinks one exact response-evidence association without deleting retained evidence.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        Guid sentEvidenceId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedVersion,
                ref operationKey,
                ref reason);
            StaffMcpInput.RequireIdentifier(sentEvidenceId, nameof(sentEvidenceId));
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await unlinkResponse.ExecuteAsync(
                new(
                    triageId,
                    sentEvidenceId,
                    expectedVersion,
                    staff.HistoryActor,
                    operationKey,
                    reason),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class TriageLinkCaseMcpTool(
    ILinkTriageCase linkCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageLinkCase,
        Title = "Link Triage case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Links Triage to one case using current Triage/case versions and the case edit lease.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid triageId,
        Guid caseId,
        long expectedTriageVersion,
        long expectedCaseVersion,
        string caseEditLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        ExecuteCaseLinkAsync(
            linkCase.ExecuteAsync,
            actorResolver,
            triageId,
            caseId,
            expectedTriageVersion,
            expectedCaseVersion,
            caseEditLeaseToken,
            operationKey,
            reason,
            cancellationToken);

    internal static Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteCaseLinkAsync(
        Func<TriageCaseLinkRequest, CancellationToken, Task> execute,
        StaffMcpActorResolver actorResolver,
        Guid triageId,
        Guid caseId,
        long expectedTriageVersion,
        long expectedCaseVersion,
        string caseEditLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            TriageMcpInput.ValidateMutation(
                triageId,
                expectedTriageVersion,
                ref operationKey,
                ref reason);
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            caseEditLeaseToken = StaffMcpInput.RequireLease(caseEditLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await execute(
                new(
                    triageId,
                    caseId,
                    expectedTriageVersion,
                    expectedCaseVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    caseEditLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class TriageUnlinkCaseMcpTool(
    IUnlinkTriageCase unlinkCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageUnlinkCase,
        Title = "Unlink Triage case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Unlinks Triage from one case using current Triage/case versions and the case edit lease.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid triageId,
        Guid caseId,
        long expectedTriageVersion,
        long expectedCaseVersion,
        string caseEditLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TriageLinkCaseMcpTool.ExecuteCaseLinkAsync(
            unlinkCase.ExecuteAsync,
            actorResolver,
            triageId,
            caseId,
            expectedTriageVersion,
            expectedCaseVersion,
            caseEditLeaseToken,
            operationKey,
            reason,
            cancellationToken);
}

[McpServerToolType]
internal sealed class TriageCompleteMcpTool(
    ICompleteTriage completeTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageComplete,
        Title = "Complete Triage",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Completes Triage after the authoritative workflow gates accept the current record.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TriageUnassignMcpTool.ExecuteMutationAsync(
            completeTriage.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            cancellationToken);
}

[McpServerToolType]
internal sealed class TriageCancelMcpTool(
    ICancelTriage cancelTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageCancel,
        Title = "Cancel Triage",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Cancels the current Triage record with an explicit reason.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TriageUnassignMcpTool.ExecuteMutationAsync(
            cancelTriage.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            cancellationToken);
}

[McpServerToolType]
internal sealed class TriageReopenMcpTool(
    IReopenTriage reopenTriage,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.TriageReopen,
        Title = "Reopen Triage",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reopens a completed or cancelled Triage record with an explicit reason.")]
    public Task<StaffMcpResult<TriageMcpRecord>> ExecuteAsync(
        Guid triageId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TriageUnassignMcpTool.ExecuteMutationAsync(
            reopenTriage.ExecuteAsync,
            actorResolver,
            triageId,
            expectedVersion,
            operationKey,
            reason,
            cancellationToken);
}
