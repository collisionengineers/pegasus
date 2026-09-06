using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

internal sealed record IntakeQueueToolItem(
    Guid ReceiptId,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    string ProcessingDecision,
    string AllocationStatus,
    string? FailureReason,
    string? AllocationSafeReason,
    Guid? CaseId,
    string? CaseReference);

internal sealed record IntakeQueueToolResult(
    IReadOnlyList<IntakeQueueToolItem> Items,
    string? Decision,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record IntakeSubmitToolResult(
    Guid ReceiptId,
    bool IsDuplicate,
    string Disposition,
    string ExternalReceiptToken,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor intake-queue tools (MCP-03): the same Core intake list
/// query and durable intake receipt submission the staff app composes,
/// guarded by the automation.intake scope. A submission is an immutable
/// source occurrence on the dedicated automation channel; custody begins only
/// at an authenticated accepted submission.
/// </summary>
[McpServerToolType]
internal sealed class IntakeMcpTools(
    IListIntakeByCursor listIntake,
    IIntakeSubmission intakeSubmission,
    TimeProvider timeProvider,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const int MaximumExternalReceiptTokenLength = 200;

    [McpServerTool(
        Name = "pegasus_intake_queue_list",
        Title = "List intake queue",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists intake receipts with processing decision and allocation state kept separate. Filters are case_created, needs_sorting, blocked_intake, unsupported, ocr_required, technical_failure, or no filter for all. Uses protected continuations; limit defaults to 50 and is at most 100.")]
    public async Task<IntakeQueueToolResult> ListAsync(
        [Description("Optional decision filter code; omit for every decision.")] string? decision = null,
        [Description("Opaque continuation returned by the preceding list call.")] string? cursor = null,
        [Description("Items to return; omit for the default 50, maximum 100.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_intake_queue_list",
            "intake-queue",
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                IntakeDecision? decisionFilter = null;
                if (!string.IsNullOrWhiteSpace(decision))
                {
                    decisionFilter = decision.Trim() switch
                    {
                        "case_created" => IntakeDecision.CaseCreated,
                        "needs_sorting" => IntakeDecision.NeedsSorting,
                        "blocked_intake" => IntakeDecision.BlockedIntake,
                        "unsupported" => IntakeDecision.Unsupported,
                        "ocr_required" => IntakeDecision.OcrRequired,
                        "technical_failure" => IntakeDecision.TechnicalFailure,
                        _ => throw new McpException("The intake decision filter is not recognized.")
                    };
                }

                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var result = await listIntake.ExecuteAsync(
                    new(context.Actor, decisionFilter, cursor, effectiveLimit),
                    cancellationToken);
                return new IntakeQueueToolResult(
                    result.Items
                        .Select(item => new IntakeQueueToolItem(
                            item.Id,
                            item.SourceFileName,
                            item.ReceivedAtUtc,
                            DecisionCode(item.Decision),
                            AllocationCode(item),
                            item.FailureReason,
                            item.AllocationState?.SafeReason,
                            item.CaseId,
                            item.CaseReference))
                        .ToArray(),
                    decisionFilter is { } filter ? DecisionCode(filter) : null,
                    result.NextCursor,
                    effectiveLimit,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_intake_submit",
        Title = "Submit intake source",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Submits one immutable intake source document (email, PDF, document, or image) into the durable intake queue on the automation channel. Content is base64 and is limited to 10 MB before decoding. The external receipt token is the durable source-occurrence identity: replaying the same token with identical content is a duplicate, and different content under the same token fails closed.")]
    public async Task<IntakeSubmitToolResult> SubmitAsync(
        [Description("The leaf file name; path components are rejected.")] string fileName,
        [Description("The source media type.")] string mediaType,
        [Description("The complete source content encoded as base64.")] string contentBase64,
        [Description("The durable source-occurrence identity for this exact submission, at most 200 characters.")] string externalReceiptToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        var normalizedToken = externalReceiptToken?.Trim();
        return await auditor.RecordAsync(
            context,
            "pegasus_intake_submit",
            normalizedToken is { Length: > 0 and <= 200 } ? normalizedToken : "invalid",
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                if (string.IsNullOrEmpty(normalizedToken)
                    || normalizedToken.Length > MaximumExternalReceiptTokenLength)
                {
                    throw new McpException(
                        "An external receipt token of at most 200 characters is required.");
                }

                var safeFileName = AutomationMcpErrors.RequireFileName(fileName);
                var safeMediaType = AutomationMcpErrors.RequireMediaType(mediaType);
                var content = AutomationMcpErrors.DecodeContent(
                    contentBase64,
                    IntakeEnvelopeLimits.MaximumContentLength,
                    "The intake source content");

                var result = await intakeSubmission.ExecuteAsync(
                    new(
                        safeFileName,
                        safeMediaType,
                        content,
                        timeProvider.GetUtcNow(),
                        $"automation:{context.ClientId}",
                        new(IntakeSourceChannel.Automation, normalizedToken)),
                    normalizedKey,
                    cancellationToken);
                return new IntakeSubmitToolResult(
                    result.StagedReceiptId,
                    result.IsDuplicate,
                    "Queued",
                    normalizedToken,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    private static string DecisionCode(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated => "case_created",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.BlockedIntake => "blocked_intake",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        _ => throw new InvalidOperationException(
            $"Unknown intake decision '{(int)decision}'.")
    };

    internal static string AllocationCode(IntakeReceiptSummary item) => item switch
    {
        { CaseId: not null } => "case_created",
        { AllocationState.Status: IntakeAllocationProjectionStatus.Pending } => "pending",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedRecoverable } => "failed_recoverable",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedBlocked } => "failed_blocked",
        { Decision: IntakeDecision.CaseCreated } => "ready_for_allocation",
        _ => "not_applicable"
    };
}
