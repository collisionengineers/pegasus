using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class CasesAcquireEditLeaseMcpTool(
    IAcquireCaseEditLease acquireLease,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesAcquireEditLease,
        Title = "Acquire case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Acquires the current short-lived edit lease for the authenticated staff actor.")]
    public Task<StaffMcpResult<CaseEditLease>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        Guid operationId,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            StaffMcpInput.RequireIdentifier(operationId, nameof(operationId));
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await acquireLease.ExecuteAsync(
                new(caseId, expectedVersion, staff.Actor, operationId.ToString("N")),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesRenewEditLeaseMcpTool(
    IRenewCaseEditLease renewLease,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesRenewEditLease,
        Title = "Renew case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Renews the authenticated holder's active case edit lease idempotently.")]
    public Task<StaffMcpResult<CaseEditLease>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        Guid operationId,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            StaffMcpInput.RequireIdentifier(operationId, nameof(operationId));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await renewLease.ExecuteAsync(
                new(caseId, expectedVersion, staff.Actor, operationId.ToString("N"), editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesReleaseEditLeaseMcpTool(
    IReleaseCaseEditLease releaseLease,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesReleaseEditLease,
        Title = "Release case edit lease",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Releases the authenticated holder's active case edit lease idempotently.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid caseId,
        Guid operationId,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(operationId, nameof(operationId));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await releaseLease.ExecuteAsync(
                new(caseId, staff.Actor, operationId.ToString("N"), editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesCreateTaskMcpTool(
    ICreateCaseTask createTask,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesCreateTask,
        Title = "Create case task",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Creates a case task using the current case version and active edit lease.")]
    public Task<StaffMcpResult<CaseTaskRecord>> ExecuteAsync(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string description,
        Guid? assigneeId,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(taskId, nameof(taskId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            description = StaffMcpInput.RequireText(description, nameof(description), 2_000);
            if (assigneeId == Guid.Empty)
            {
                throw new ModelContextProtocol.McpException(
                    "'assigneeId' must be a non-empty identifier when supplied.");
            }
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await createTask.ExecuteAsync(
                new(
                    caseId,
                    taskId,
                    expectedCaseVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    description,
                    assigneeId),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesAssignTaskMcpTool(
    IAssignCaseTask assignTask,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesAssignTask,
        Title = "Assign case task",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Assigns or unassigns a case task using current case/task versions and the active edit lease.")]
    public Task<StaffMcpResult<CaseTaskRecord>> ExecuteAsync(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid? assigneeId,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            ValidateTaskMutation(
                caseId,
                taskId,
                expectedCaseVersion,
                expectedTaskVersion,
                ref operationKey,
                ref reason,
                ref editLeaseToken);
            if (assigneeId == Guid.Empty)
            {
                throw new ModelContextProtocol.McpException(
                    "'assigneeId' must be a non-empty identifier when supplied.");
            }
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await assignTask.ExecuteAsync(
                new(
                    caseId,
                    taskId,
                    expectedCaseVersion,
                    expectedTaskVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    assigneeId),
                cancellationToken);
        });

    private static void ValidateTaskMutation(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long expectedTaskVersion,
        ref string operationKey,
        ref string reason,
        ref string editLeaseToken)
    {
        StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
        StaffMcpInput.RequireIdentifier(taskId, nameof(taskId));
        StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
        StaffMcpInput.RequireVersion(expectedTaskVersion, nameof(expectedTaskVersion));
        operationKey = StaffMcpInput.RequireOperationKey(operationKey);
        reason = StaffMcpInput.RequireReason(reason);
        editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
    }

    internal static void ValidateExistingTaskMutation(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long expectedTaskVersion,
        ref string operationKey,
        ref string reason,
        ref string editLeaseToken) =>
        ValidateTaskMutation(
            caseId,
            taskId,
            expectedCaseVersion,
            expectedTaskVersion,
            ref operationKey,
            ref reason,
            ref editLeaseToken);
}

[McpServerToolType]
internal sealed class CasesHoldMcpTool(
    IHoldCase holdCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesHold,
        Title = "Hold case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Places a case on hold using the current version, reason and active edit lease.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await holdCase.ExecuteAsync(
                new(caseId, expectedVersion, staff.Actor, key, normalizedReason, lease),
                cancellationToken)),
            cancellationToken);

    internal static Task<StaffMcpResult<T>> ExecuteCaseMutationAsync<T>(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        StaffMcpActorResolver actorResolver,
        Func<StaffMcpActor, string, string, string, Task<T>> execute,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await execute(staff, operationKey, reason, editLeaseToken);
        });
}

[McpServerToolType]
internal sealed class CasesReleaseHoldMcpTool(
    IReleaseCase releaseCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesReleaseHold,
        Title = "Release case hold",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Releases a case hold using the current version, reason and active edit lease.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await releaseCase.ExecuteAsync(
                new ChangeCaseStateRequest(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease),
                cancellationToken)),
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesTransitionMcpTool(
    ITransitionCase transitionCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesTransition,
        Title = "Transition case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Transitions a case through the authoritative Core readiness policy.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseTransitionDestination destination,
        CaseReadinessEvidence? readiness,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await transitionCase.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    destination,
                    readiness),
                cancellationToken)),
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesCloseMcpTool(
    ICloseCase closeCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesClose,
        Title = "Close case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Closes a case with an explicit terminal outcome and reason.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseClosureOutcome outcome,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await closeCase.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    outcome),
                cancellationToken)),
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesReopenMcpTool(
    IReopenCase reopenCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesReopen,
        Title = "Reopen case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reopens a case to an explicit destination through the Core readiness policy.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseReopenDestination destination,
        CaseReadinessEvidence? readiness,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await reopenCase.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    destination,
                    readiness),
                cancellationToken)),
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesArchiveMcpTool(
    IArchiveCase archiveCase,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesArchive,
        Title = "Archive case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Archives a closed case as application-read-only with a permanent reason.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) => CaseMcpWorkflowSummary.From(
                await archiveCase.ExecuteAsync(
                new(caseId, expectedVersion, staff.Actor, key, normalizedReason, lease),
                cancellationToken)),
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesCreateLinkedReplacementMcpTool(
    ICreateLinkedReplacement createLinkedReplacement,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesCreateLinkedReplacement,
        Title = "Create linked replacement case",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Creates the immutable linked replacement for a case created under the wrong principal.")]
    public Task<StaffMcpResult<IntakeAcceptanceMcpReceipt>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string replacementPrincipalCode,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            async (staff, key, normalizedReason, lease) =>
            {
                replacementPrincipalCode = StaffMcpInput.RequireText(
                    replacementPrincipalCode,
                    nameof(replacementPrincipalCode),
                    20);
                var outcome = await createLinkedReplacement.ExecuteAsync(
                    new(
                        caseId,
                        expectedVersion,
                        staff.Actor,
                        key,
                        normalizedReason,
                        lease,
                        replacementPrincipalCode),
                    cancellationToken);
                return new IntakeAcceptanceMcpReceipt(
                    outcome.Identity,
                    outcome.InitialState,
                    outcome.CustodyState,
                    outcome.IsDuplicate);
            },
            cancellationToken);
}

[McpServerToolType]
internal sealed class CasesCompleteTaskMcpTool(
    ICompleteCaseTask completeTask,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesCompleteTask,
        Title = "Complete case task",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Completes a case task using current case/task versions and the active edit lease.")]
    public Task<StaffMcpResult<CaseTaskRecord>> ExecuteAsync(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            CasesAssignTaskMcpTool.ValidateExistingTaskMutation(
                caseId,
                taskId,
                expectedCaseVersion,
                expectedTaskVersion,
                ref operationKey,
                ref reason,
                ref editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await completeTask.ExecuteAsync(
                new(
                    caseId,
                    taskId,
                    expectedCaseVersion,
                    expectedTaskVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesCancelTaskMcpTool(
    ICancelCaseTask cancelTask,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesCancelTask,
        Title = "Cancel case task",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Cancels a case task using current case/task versions, reason and the active edit lease.")]
    public Task<StaffMcpResult<CaseTaskRecord>> ExecuteAsync(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            CasesAssignTaskMcpTool.ValidateExistingTaskMutation(
                caseId,
                taskId,
                expectedCaseVersion,
                expectedTaskVersion,
                ref operationKey,
                ref reason,
                ref editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await cancelTask.ExecuteAsync(
                new(
                    caseId,
                    taskId,
                    expectedCaseVersion,
                    expectedTaskVersion,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class CasesRecordEngineerFindingMcpTool(
    IRecordEngineerFinding recordEngineerFinding,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.CasesRecordEngineerFinding,
        Title = "Record Engineer finding",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records the assigned Engineer's finding through the authoritative role and case policy.")]
    public Task<StaffMcpResult<CaseIdentity>> ExecuteAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        AuditAssessment assessment,
        CancellationToken cancellationToken) =>
        CasesHoldMcpTool.ExecuteCaseMutationAsync(
            caseId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            (staff, key, normalizedReason, lease) => recordEngineerFinding.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    assessment),
                cancellationToken),
            cancellationToken);
}
