using System.ComponentModel;
using Pegasus.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Intake;
using Pegasus.Web.Pages.Mail;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record MailToolMailbox(
    string MailboxId,
    string MailboxAddress,
    bool IsPolled);

internal sealed record MailToolFreshness(
    string State,
    DateTimeOffset? LastSuccessfulUpdateAtUtc);

internal sealed record MailToolSummary(
    Guid Id,
    string MailboxId,
    string MailboxAddress,
    string? SenderAddress,
    string? SenderDisplayName,
    string? EffectiveSenderAddress,
    string? Subject,
    string? BodyExcerpt,
    DateTimeOffset ReceivedAtUtc,
    bool IsRead,
    int AttachmentCount,
    string? ProcessingOutcome,
    Guid? IntakeReceiptId,
    Guid? CaseId,
    string? CaseReference);

internal sealed record MailToolPage(
    IReadOnlyList<MailToolSummary> Items,
    string? Continuation,
    bool HasUnretainedHistory,
    IReadOnlyList<MailToolMailbox> Mailboxes,
    MailToolFreshness Freshness);

internal sealed record MailToolCategory(
    string Direction,
    string Name,
    string? Subtype,
    bool IsOther,
    string? OtherReasoning);

internal sealed record MailToolPredicate(
    string Key,
    bool Matched,
    string Detail);

internal sealed record MailToolClassificationResult(
    string Outcome,
    MailToolCategory? Category,
    IReadOnlyList<string> AmbiguousCandidates,
    IReadOnlyList<MailToolPredicate> Predicates,
    string Reason,
    string PolicyKey,
    int PolicyVersion);

internal sealed record MailToolCorrectionHistoryEntry(
    int Version,
    MailToolClassificationResult Before,
    MailToolClassificationResult After,
    string Actor,
    string Reason,
    DateTimeOffset CorrectedAtUtc);

internal sealed record MailToolClassification(
    int Version,
    MailToolClassificationResult Current,
    string CurrentActor,
    DateTimeOffset CurrentDecidedAtUtc,
    string OperationalDestination,
    IReadOnlyList<MailToolCorrectionHistoryEntry> History,
    IReadOnlyList<MailClassificationSelection.SelectionOption> CorrectionOptions);

internal sealed record MailToolAttachment(
    string FileName,
    string MediaType,
    long ContentLength);

internal sealed record MailToolThreadEntry(
    Guid Id,
    string? SenderDisplayName,
    string? SenderAddress,
    string? Subject,
    DateTimeOffset ReceivedAtUtc);

internal sealed record MailToolDetail(
    MailToolSummary Summary,
    string Folder,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    string? BodyPlainText,
    IReadOnlyList<MailToolAttachment> Attachments,
    IReadOnlyList<MailToolThreadEntry> Thread,
    string? ClassificationOutcome,
    string? RouteDisposition,
    MailToolClassification? Classification,
    string CorrelationId);

/// <summary>
/// The classified-email workspace for the Automation Actor: the same Core
/// queries and the same correction command the staff mail pages call, behind
/// the per-area <c>automation.mail</c> scope. Reads mirror the workspace
/// list and message detail; the only mutation is the staff-equivalent
/// classification correction. No Outlook or mailbox mutation exists here —
/// the retained read model is Pegasus-side only, and transport mutation
/// remains a separately approved capability.
/// </summary>
[McpServerToolType]
internal sealed class MailMcpTools(
    ListRetainedMail listRetainedMail,
    GetRetainedMail getRetainedMail,
    GetRetainedMailFreshness getFreshness,
    CorrectRetainedMailClassification correctClassification,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    ICursorProtector cursors)
{
    [McpServerTool(
        Name = "pegasus_mail_list",
        Title = "List retained mail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the retained mail workspace, newest first: one page of messages with the mailboxes that hold retained mail and how fresh the workspace is. Defaults to every mailbox and the inbox scope. Reads the retained record only; nothing is marked read in any mailbox.")]
    public async Task<MailToolPage> ListAsync(
        [Description("Optional exact mailbox identity from the mailboxes list. Omit for every mailbox.")] string? mailbox = null,
        [Description("Optional folder scope: inbox, sent, or deleted. Defaults to inbox.")] string? folder = null,
        [Description("Opaque continuation returned by the preceding call.")] string? continuation = null,
        [Description("Page size from 1 to 100; 0 selects 50.")] int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.MailScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_mail_list",
            "mail",
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                if (!IndexModel.TryParseFolder(
                        string.IsNullOrWhiteSpace(folder) ? null : folder.Trim(),
                        out var folderScope))
                {
                    throw new McpException("The folder scope must be inbox, sent, or deleted.");
                }

                var scope = new MailWorkspaceScope(
                    Guid.TryParse(mailbox, out var mailboxId) && mailboxId != Guid.Empty
                        ? mailboxId
                        : null,
                    folderScope);
                var limit = CursorPaging.NormalizeLimit(pageSize == 0 ? null : pageSize);
                var cursorScope = CursorPaging.CreateScope(
                    "pegasus_mail_list", context.Actor, scope.MailboxId?.ToString("D"),
                    scope.Folder.ToString(), "received-id-desc");
                DateTimeOffset? beforeReceived = null;
                Guid? beforeId = null;
                if (!string.IsNullOrWhiteSpace(continuation))
                {
                    var position = cursors.Unprotect(continuation, cursorScope);
                    beforeReceived = CursorPaging.DecodeUtcTimestamp(position.SortKey);
                    beforeId = position.Id;
                }
                var page = await listRetainedMail.ExecuteCursorAsync(
                    context.Actor, scope, beforeReceived, beforeId, limit, cancellationToken);
                var next = page.HasMore && page.Items.Count > 0
                    ? cursors.Protect(cursorScope,
                        CursorPaging.EncodeUtcTimestamp(page.Items[^1].ReceivedAtUtc), page.Items[^1].Id)
                    : null;
                var mailboxes = await listRetainedMail.ListMailboxesAsync(
                    context.Actor,
                    cancellationToken);
                var freshness = await getFreshness.ExecuteAsync(context.Actor, cancellationToken);
                return new MailToolPage(
                    page.Items.Select(Map).ToArray(),
                    next,
                    page.HasUnretainedHistory,
                    mailboxes.Select(item => new MailToolMailbox(
                        item.MailboxId.ToString("D"),
                        item.MailboxAddress,
                        item.IsPolled)).ToArray(),
                    new(
                        IndexModel.FreshnessStatus(freshness.State),
                        freshness.LastSuccessfulUpdateAtUtc));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_mail_get",
        Title = "Get retained mail detail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one retained message: recipients, body text, attachment names, thread, the versioned classification decision with its permanent correction history and operational destination, and the canonical correction options. Attachments are listed by name, type and size; their content is not returned here — a message attached to a Case exposes its documents through the document tools.")]
    public async Task<MailToolDetail> GetAsync(
        [Description("The retained message identifier from pegasus_mail_list.")] Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.MailScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_mail_get",
            messageId.ToString("D"),
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var detail = await getRetainedMail.ExecuteAsync(
                    context.Actor,
                    AutomationMcpErrors.RequireId(messageId, "retained message identifier"),
                    cancellationToken)
                    ?? throw new McpException("The retained message was not found.");
                return new MailToolDetail(
                    Map(detail.Summary),
                    IndexModel.FolderCode(detail.Folder),
                    detail.ToAddresses,
                    detail.CcAddresses,
                    detail.BodyPlainText,
                    detail.Attachments.Select(item => new MailToolAttachment(
                        item.FileName,
                        item.MediaType,
                        item.ContentLength)).ToArray(),
                    detail.Thread.Select(item => new MailToolThreadEntry(
                        item.Id,
                        item.SenderDisplayName,
                        item.SenderAddress,
                        item.Subject,
                        item.ReceivedAtUtc)).ToArray(),
                    detail.ClassificationOutcome?.ToString(),
                    detail.RouteDisposition?.ToString(),
                    detail.Classification is { } dossier ? Map(dossier) : null,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_mail_correct_classification",
        Title = "Correct mail classification",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Corrects one retained message's classification using the same versioned Core command as the staff workspace. Requires the exact classification key from the correction options (Other keys also need otherName and otherReasoning), a reason, the expected classification version, and a mcp:-prefixed operation key. The prior decision stays in permanent history.")]
    public async Task<MailToolClassification> CorrectClassificationAsync(
        [Description("The retained message identifier from pegasus_mail_list.")] Guid messageId,
        [Description("The classification version last read, for optimistic concurrency.")] int expectedClassificationVersion,
        [Description("A correction options key, for example received:NewInstructionReceived:inspection, sent:ReportSent, other-received.")] string classificationKey,
        [Description("Why this classification is being corrected (1 to 500 characters).")] string reason,
        [Description("Caller idempotency key, prefixed mcp:.")] string operationKey,
        [Description("New category name; required only with an other-received or other-sent key.")] string? otherName = null,
        [Description("Why no existing category fits; required only with an other-received or other-sent key.")] string? otherReasoning = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.MailScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_mail_correct_classification",
            messageId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                if (!MailClassificationSelection.TryParse(
                        classificationKey?.Trim(),
                        otherName,
                        otherReasoning,
                        out var category))
                {
                    throw new McpException(
                        "The classification key is not a canonical correction option, or the Other details are missing or outside their bounds.");
                }

                var dossier = await correctClassification.ExecuteAsync(
                    context.Actor,
                    new(
                        AutomationMcpErrors.RequireId(messageId, "retained message identifier"),
                        expectedClassificationVersion,
                        category!,
                        reason),
                    cancellationToken)
                    ?? throw new McpException(
                        "The retained message was not found or has no classification decision to correct.");
                return Map(dossier);
            }),
            cancellationToken);
    }

    private static MailToolSummary Map(RetainedMailSummary summary) => new(
        summary.Id,
        summary.MailboxId.ToString("D"),
        summary.MailboxAddress,
        summary.SenderAddress,
        summary.SenderDisplayName,
        summary.EffectiveSenderAddress,
        summary.Subject,
        summary.BodyExcerpt,
        summary.ReceivedAtUtc,
        summary.IsRead,
        summary.AttachmentCount,
        summary.ProcessingOutcome?.ToString(),
        summary.IntakeReceiptId,
        summary.CaseId,
        summary.CaseReference);

    private static MailToolClassification Map(MailClassificationDossier dossier) => new(
        dossier.Version,
        Map(dossier.Current),
        dossier.CurrentActor,
        dossier.CurrentDecidedAtUtc,
        MailOperationalDestinationPolicy.Map(dossier.Current).Destination.ToString(),
        dossier.History.Select(entry => new MailToolCorrectionHistoryEntry(
            entry.Version,
            Map(entry.Before),
            Map(entry.After),
            entry.Actor,
            entry.Reason,
            entry.CorrectedAtUtc)).ToArray(),
        MailClassificationSelection.Options);

    private static MailToolClassificationResult Map(MailClassificationResult result) => new(
        result.Outcome.ToString(),
        result.Category is { } category
            ? new(
                category.Direction.ToString(),
                category.Name,
                category.Subtype,
                category.IsOther,
                category.OtherReasoning)
            : null,
        result.AmbiguousCandidates,
        result.Predicates.Select(predicate => new MailToolPredicate(
            predicate.Key,
            predicate.Matched,
            predicate.Detail)).ToArray(),
        result.Reason,
        result.PolicyKey,
        result.PolicyVersion);
}
