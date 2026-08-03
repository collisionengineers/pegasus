using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

internal sealed record IntakeQueueToolItem(
    Guid ReceiptId,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    string Decision,
    string? FailureReason);

internal sealed record IntakeQueueToolResult(
    IReadOnlyList<IntakeQueueToolItem> Items,
    string? Decision,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
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
    IListIntake listIntake,
    IIntakeSubmission intakeSubmission,
    TimeProvider timeProvider,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const int MaximumPageSize = 50;
    private const int MaximumExternalReceiptTokenLength = 200;

    [McpServerTool(
        Name = "pegasus_intake_queue_list",
        Title = "List intake queue",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists intake receipts with the same decision filters as the staff intake queue: draft_ready, needs_sorting, blocked_intake, unsupported, ocr_required, technical_failure, or no filter for all. Page size is capped at 50.")]
    public async Task<IntakeQueueToolResult> ListAsync(
        [Description("Optional decision filter code; omit for every decision.")] string? decision,
        [Description("1-based page number.")] int page,
        [Description("Page size between 1 and 50; 0 selects the default of 25.")] int pageSize,
        CancellationToken cancellationToken)
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
                        "draft_ready" => IntakeDecision.DraftReady,
                        "needs_sorting" => IntakeDecision.NeedsSorting,
                        "blocked_intake" => IntakeDecision.BlockedIntake,
                        "unsupported" => IntakeDecision.Unsupported,
                        "ocr_required" => IntakeDecision.OcrRequired,
                        "technical_failure" => IntakeDecision.TechnicalFailure,
                        _ => throw new McpException("The intake decision filter is not recognized.")
                    };
                }

                var effectivePage = page == 0 ? 1 : page;
                var effectivePageSize = pageSize == 0 ? 25 : pageSize;
                if (effectivePageSize is < 1 or > MaximumPageSize)
                {
                    throw new McpException(
                        $"The page size must be between 1 and {MaximumPageSize}.");
                }

                var result = await listIntake.ExecuteAsync(
                    new(context.Actor, decisionFilter, effectivePage, effectivePageSize),
                    cancellationToken);
                return new IntakeQueueToolResult(
                    result.Items
                        .Select(item => new IntakeQueueToolItem(
                            item.Id,
                            item.SourceFileName,
                            item.ReceivedAtUtc,
                            DecisionCode(item.Decision),
                            item.FailureReason))
                        .ToArray(),
                    decisionFilter is { } filter ? DecisionCode(filter) : null,
                    result.Page,
                    result.PageSize,
                    result.TotalCount,
                    result.TotalPages,
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
        CancellationToken cancellationToken)
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
                    result.ReceiptId,
                    result.IsDuplicate,
                    result.Disposition.ToString(),
                    normalizedToken,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    private static string DecisionCode(IntakeDecision decision) => decision switch
    {
        IntakeDecision.DraftReady => "draft_ready",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.BlockedIntake => "blocked_intake",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        _ => throw new InvalidOperationException(
            $"Unknown intake decision '{(int)decision}'.")
    };
}
