using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

internal sealed record IntakeAssetMcpSummary(
    Guid Id,
    string SourceLabel,
    string FileName,
    string MediaType,
    IntakeAssetKind Kind,
    IntakeAssetDisposition Disposition,
    long ContentLength,
    string ContentHash,
    int? PageNumber,
    IntakeAssetBounds? Bounds,
    int? WidthPixels,
    int? HeightPixels);

internal sealed record IntakeEvidenceMcpSummary(
    IntakeEvidenceSource Source,
    IntakeEvidenceStrength Strength,
    IntakeEvidenceFinding Finding,
    string Signal,
    string? MatcherKey,
    int? MatcherVersion);

internal sealed record IntakeMcpDetail(
    Guid Id,
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset ProcessedAtUtc,
    IntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<IntakeEvidenceMcpSummary> Evidence,
    IReadOnlyList<InstructionReviewField> Fields,
    InstructionDraft? InstructionDraft,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    bool IsDuplicate,
    string SourceReaderKey,
    string SourceReaderVersion,
    string? ExtractionPolicyKey,
    int? ExtractionPolicyVersion,
    IReadOnlyList<IntakeAssetMcpSummary> Assets,
    IReadOnlyList<ScannedPdfOcrCandidate> OcrCandidates,
    long Version,
    Guid? CurrentCaseId,
    bool IsTruncated)
{
    private const int MaximumCollectionItems = 100;

    public static IntakeMcpDetail From(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var assets = receipt.AssetRecords;
        var ocrCandidates = receipt.ScannedPdfPages;
        var isTruncated = receipt.Evidence.Count > MaximumCollectionItems
            || receipt.Fields.Count > MaximumCollectionItems
            || receipt.MissingFields.Count > MaximumCollectionItems
            || assets.Count > MaximumCollectionItems
            || ocrCandidates.Count > MaximumCollectionItems;
        return new(
            receipt.Id,
            DocumentMcpContent.SanitizeFileName(receipt.SourceFileName),
            receipt.MediaType,
            receipt.SourceLength,
            receipt.SourceHash,
            receipt.ReceivedAtUtc,
            receipt.ProcessedAtUtc,
            receipt.Decision,
            receipt.DecisionReason,
            receipt.Evidence.Take(MaximumCollectionItems).Select(item => new IntakeEvidenceMcpSummary(
                item.Source,
                item.Strength,
                item.Finding,
                item.Signal,
                item.MatcherKey,
                item.MatcherVersion)).ToArray(),
            receipt.Fields.Take(MaximumCollectionItems).ToArray(),
            receipt.InstructionDraft,
            receipt.MissingFields.Take(MaximumCollectionItems).ToArray(),
            receipt.FailureCode,
            receipt.IsDuplicate,
            receipt.SourceReaderKey,
            receipt.SourceReaderVersion,
            receipt.ExtractionPolicyKey,
            receipt.ExtractionPolicyVersion,
            assets.Take(MaximumCollectionItems).Select(item => new IntakeAssetMcpSummary(
                item.Id,
                item.SourceLabel,
                DocumentMcpContent.SanitizeFileName(item.FileName),
                item.MediaType,
                item.Kind,
                item.Disposition,
                item.ContentLength,
                item.ContentHash,
                item.PageNumber,
                item.Bounds,
                item.WidthPixels,
                item.HeightPixels)).ToArray(),
            ocrCandidates.Take(MaximumCollectionItems).ToArray(),
            receipt.Version,
            receipt.CurrentCaseId,
            isTruncated);
    }
}

internal sealed record IntakeAcceptanceMcpReceipt(
    CaseIdentity Identity,
    CaseInitialState InitialState,
    CaseCustodyState CustodyState,
    bool IsDuplicate);

[McpServerToolType]
internal sealed class IntakeListMcpTool(
    IListIntake listIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeList,
        Title = "List intake",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists one bounded page of intake records authorized for the current staff member.")]
    public Task<StaffMcpResult<IntakeListPage>> ExecuteAsync(
        IntakeDecision? decision,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequirePage(page, pageSize);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await listIntake.ExecuteAsync(
                new(staff.Actor, decision, page, pageSize),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class IntakeGetMcpTool(
    IGetIntake getIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeGet,
        Title = "Get intake",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets bounded intake evidence and review data without source bytes or custody coordinates.")]
    public async Task<StaffMcpResult<IntakeMcpDetail>> ExecuteAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
        var result = await StaffMcpCall.ExecuteAsync(async () =>
        {
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await getIntake.ExecuteAsync(
                new(receiptId, staff.Actor),
                cancellationToken);
        });
        return result.Outcome == StaffMcpCallOutcome.Succeeded
            ? result.Value is { } receipt
                ? StaffMcpResult<IntakeMcpDetail>.Succeeded(IntakeMcpDetail.From(receipt))
                : StaffMcpResult<IntakeMcpDetail>.NotFound()
            : new(result.Outcome, null, result.ErrorCode, result.CurrentVersion);
    }
}

[McpServerToolType]
internal sealed class IntakeResolveMcpTool(
    IResolveIntake resolveIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeResolve,
        Title = "Resolve intake",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Resolves an intake draft or blocks it using the observed version and an idempotency key.")]
    public Task<StaffMcpResult<IntakeMcpDetail>> ExecuteAsync(
        Guid receiptId,
        long expectedVersion,
        string operationKey,
        string reason,
        IntakeResolutionKind kind,
        InstructionDraft? correctedDraft,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var receipt = await resolveIntake.ExecuteAsync(
                new(receiptId, expectedVersion, staff.Actor, operationKey, reason, kind, correctedDraft),
                cancellationToken);
            return IntakeMcpDetail.From(receipt);
        });
}

[McpServerToolType]
internal sealed class IntakeReevaluateMcpTool(
    IReevaluateIntake reevaluateIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeReevaluate,
        Title = "Reevaluate intake",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Schedules intake reevaluation using the observed version and an idempotency key.")]
    public Task<StaffMcpResult<IntakeMcpDetail>> ExecuteAsync(
        Guid receiptId,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var receipt = await reevaluateIntake.ExecuteAsync(
                new(receiptId, expectedVersion, staff.Actor, operationKey, reason),
                cancellationToken);
            return IntakeMcpDetail.From(receipt);
        });
}

[McpServerToolType]
internal sealed class IntakeAcceptMcpTool(
    IAcceptIntake acceptIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeAccept,
        Title = "Accept intake",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Accepts reviewed intake through the authoritative case-allocation transaction.")]
    public Task<StaffMcpResult<IntakeAcceptanceMcpReceipt>> ExecuteAsync(
        Guid receiptId,
        long expectedVersion,
        string operationKey,
        string reason,
        CaseType caseType,
        string principalCode,
        CaseCompleteness completeness,
        Guid? standaloneAuditEvidenceId,
        DateOnly? acceptedInspectionDeadline,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            principalCode = StaffMcpInput.RequireText(principalCode, nameof(principalCode), 20);
            if (standaloneAuditEvidenceId == Guid.Empty)
            {
                throw new ModelContextProtocol.McpException(
                    "'standaloneAuditEvidenceId' must be a non-empty identifier when supplied.");
            }
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var outcome = await acceptIntake.ExecuteAsync(
                new(
                    receiptId,
                    expectedVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    caseType,
                    principalCode,
                    completeness,
                    standaloneAuditEvidenceId,
                    acceptedInspectionDeadline),
                cancellationToken);
            return new IntakeAcceptanceMcpReceipt(
                outcome.Identity,
                outcome.InitialState,
                outcome.CustodyState,
                outcome.IsDuplicate);
        });
}

[McpServerToolType]
internal sealed class IntakeLinkCaseMcpTool(
    ILinkIntake linkIntake,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeLinkCase,
        Title = "Link intake to case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Links intake to an existing case using both observed versions and the active case edit lease.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid receiptId,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedIntakeVersion, nameof(expectedIntakeVersion));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await linkIntake.ExecuteAsync(
                new(
                    receiptId,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    staff.Actor,
                    operationKey,
                    reason),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class IntakeUnlinkCaseMcpTool(
    IReverseIntakeLink reverseIntakeLink,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.IntakeUnlinkCase,
        Title = "Unlink intake from case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reverses a manual intake link using both observed versions and the active case edit lease.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid receiptId,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(receiptId, nameof(receiptId));
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedIntakeVersion, nameof(expectedIntakeVersion));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await reverseIntakeLink.ExecuteAsync(
                new(
                    receiptId,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    staff.Actor,
                    operationKey,
                    reason),
                cancellationToken);
        });
}
