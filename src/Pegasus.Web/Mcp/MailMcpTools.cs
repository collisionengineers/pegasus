using System.ComponentModel;
using Pegasus.Core;
using Pegasus.Core.Cases;
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
    string? CaseReference,
    bool IsTriageCase);

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
    int PolicyVersion,
    string? CaseType);

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

internal sealed record MailToolFolderRecommendation(
    string? FolderType,
    string PolicyKey,
    int PolicyVersion,
    string Reason,
    int? MailboxVersion,
    bool CanMove);

internal sealed record MailToolFolderMove(
    string Outcome,
    string FolderType,
    string Reason,
    DateTimeOffset RecordedAtUtc,
    string? FailureReason);

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
    MailToolFolderRecommendation? FolderRecommendation,
    MailToolFolderMove? LatestFolderMove,
    string CorrelationId);

internal sealed record MailActionToolResult(
    Guid MessageId,
    string Action,
    string? MoveOutcome,
    string? MoveFolderType,
    string? MoveFailureReason,
    bool? IsDismissed,
    bool IsReplay,
    string OperationKey,
    string CorrelationId);

internal enum MailAction
{
    MoveFolder,
    Dismiss,
    Restore
}

/// <summary>
/// The classified-email workspace for the Automation Actor: the same Core
/// queries and the same commands the staff mail pages call, behind the
/// per-area <c>automation.mail</c> scope. Reads mirror the workspace list and
/// message detail; the mutations are the staff-equivalent classification
/// correction, Dismiss and Restore (Pegasus-side only), and the confirmed move
/// to the recommended Outlook folder (ADR-0064). Nothing here sends mail:
/// sending is <c>pegasus_mail_send</c>, under <c>automation.send</c>.
/// </summary>
[McpServerToolType]
internal sealed class MailMcpTools(
    ListRetainedMail listRetainedMail,
    GetRetainedMail getRetainedMail,
    GetRetainedMailFreshness getFreshness,
    CorrectRetainedMailClassification correctClassification,
    MoveRetainedMailFolder moveFolder,
    IDismissRetainedMail dismiss,
    IRestoreRetainedMail restore,
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
    [Description("Lists the retained mail workspace, newest first: one page of messages with the mailboxes that hold retained mail and how fresh the workspace is. Each message names its Case by caseId and caseReference; isTriageCase is true when that Case is a Triage Case, whose reference starts t. and which is not an instruction Case. Defaults to every mailbox and the inbox scope. Reads the retained record only; nothing is marked read in any mailbox.")]
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
    [Description("Gets one retained message: recipients, body text, attachment names, thread, the versioned classification decision with its permanent correction history and operational destination, the canonical correction options, the Outlook folder recommendation (with the policy and mailbox versions pegasus_mail_action move_folder confirms) and the latest folder move. Attachments are listed by name, type and size; their content is not returned here — a message attached to a Case exposes its documents through the document tools.")]
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
                    detail.FolderRecommendation is { } recommendation
                        ? new MailToolFolderRecommendation(
                            recommendation.FolderType?.ToString(),
                            recommendation.PolicyKey,
                            recommendation.PolicyVersion,
                            recommendation.Reason,
                            recommendation.MailboxVersion,
                            recommendation.CanMove)
                        : null,
                    detail.LatestFolderMove is { } move
                        ? new MailToolFolderMove(
                            move.Outcome.ToString(),
                            move.FolderType.ToString(),
                            move.Reason,
                            move.RecordedAtUtc,
                            move.FailureReason)
                        : null,
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
    [Description("Corrects one retained message's classification using the same versioned Core command as the staff workspace. Requires the exact classification key from the message's correction options (Other keys also need otherName and otherReasoning; a received:NewInstructionReceived key also needs workType), a reason, the expected classification version, and a mcp:-prefixed operation key. The prior decision stays in permanent history.")]
    public async Task<MailToolClassification> CorrectClassificationAsync(
        [Description("The retained message identifier from pegasus_mail_list.")] Guid messageId,
        [Description("The classification version last read, for optimistic concurrency.")] int expectedClassificationVersion,
        [Description("A correction options key, for example received:NewInstructionReceived:inspection, sent:ReportSent, other-received.")] string classificationKey,
        [Description("Why this classification is being corrected (1 to 500 characters).")] string reason,
        [Description("Caller idempotency key, prefixed mcp:.")] string operationKey,
        [Description("New category name; required only with an other-received or other-sent key.")] string? otherName = null,
        [Description("Why no existing category fits; required only with an other-received or other-sent key.")] string? otherReasoning = null,
        [Description("The case type a New instruction names: Inspection, Audit or InspectionAndAudit; required only with a received:NewInstructionReceived key.")] string? workType = null,
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
                CaseType? caseType = null;
                if (!string.IsNullOrWhiteSpace(workType)
                    && !MailClassificationSelection.TryParseWorkType(workType.Trim(), out caseType))
                {
                    throw new McpException("The work type is not Inspection, Audit or InspectionAndAudit.");
                }

                var dossier = await correctClassification.ExecuteAsync(
                    context.Actor,
                    new(
                        AutomationMcpErrors.RequireId(messageId, "retained message identifier"),
                        expectedClassificationVersion,
                        category!,
                        reason,
                        caseType),
                    cancellationToken)
                    ?? throw new McpException(
                        "The retained message was not found or has no classification decision to correct.");
                return Map(dossier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_mail_action",
        Title = "Move or dismiss retained mail",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("One staff mail act on a retained message, through the same Core command as the message page. move_folder moves the message in Outlook to the folder its classification recommends, confirming the classification version, recommendation policy key and version and mailbox version read from pegasus_mail_get, with a reason; the outcome is Succeeded, Failed (retry with a new operation key) or Uncertain (replay the same operation key to check where the message is). dismiss takes the message out of the incoming scopes without classifying or linking it, and restore brings a dismissed message back; neither touches Outlook, deletes anything or closes an open Unidentified item. Requires a mcp:-prefixed operation key; nothing here sends mail.")]
    public async Task<MailActionToolResult> ActAsync(
        [Description("The retained message identifier from pegasus_mail_list.")] Guid messageId,
        [Description("move_folder, dismiss or restore.")] string action,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("move_folder only: the classification version read from pegasus_mail_get.")] int? expectedClassificationVersion = null,
        [Description("move_folder only: folderRecommendation.policyKey from pegasus_mail_get.")] string? expectedRecommendationPolicyKey = null,
        [Description("move_folder only: folderRecommendation.policyVersion from pegasus_mail_get.")] int? expectedRecommendationPolicyVersion = null,
        [Description("move_folder only: folderRecommendation.mailboxVersion from pegasus_mail_get.")] int? expectedMailboxVersion = null,
        [Description("move_folder only: why the message is moved, 1 to 500 characters.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.MailScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_mail_action",
            messageId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var id = AutomationMcpErrors.RequireId(messageId, "retained message identifier");
                var parsed = ParseMailAction(action);
                if (parsed != MailAction.MoveFolder
                    && (expectedClassificationVersion is not null
                        || expectedRecommendationPolicyKey is not null
                        || expectedRecommendationPolicyVersion is not null
                        || expectedMailboxVersion is not null
                        || reason is not null))
                {
                    throw new McpException(
                        "dismiss and restore take no versions or reason; those belong to move_folder.");
                }

                var correlation = AutomationMcpAuditor.CorrelationId(context, key);
                if (parsed == MailAction.MoveFolder)
                {
                    var moved = await moveFolder.ExecuteAsync(
                        context.Actor,
                        new(
                            id,
                            expectedClassificationVersion
                                ?? throw new McpException("move_folder needs expectedClassificationVersion."),
                            expectedRecommendationPolicyKey
                                ?? throw new McpException("move_folder needs expectedRecommendationPolicyKey."),
                            expectedRecommendationPolicyVersion
                                ?? throw new McpException("move_folder needs expectedRecommendationPolicyVersion."),
                            expectedMailboxVersion
                                ?? throw new McpException("move_folder needs expectedMailboxVersion."),
                            AutomationMcpErrors.DeriveOperationGuid("mail-folder-move", context.Actor, key)
                                .ToString("D"),
                            reason ?? throw new McpException("move_folder needs reason.")),
                        cancellationToken)
                        ?? throw new McpException("The retained message was not found.");
                    return new MailActionToolResult(
                        id,
                        "move_folder",
                        moved.Outcome.ToString(),
                        moved.FolderType.ToString(),
                        moved.FailureReason,
                        null,
                        moved.IsReplay,
                        key,
                        correlation);
                }

                var dismissal = (parsed == MailAction.Dismiss
                    ? await dismiss.ExecuteAsync(new(id, context.Actor, key), cancellationToken)
                    : await restore.ExecuteAsync(new(id, context.Actor, key), cancellationToken))
                    ?? throw new McpException("The retained message was not found.");
                return new MailActionToolResult(
                    id,
                    parsed == MailAction.Dismiss ? "dismiss" : "restore",
                    null,
                    null,
                    null,
                    dismissal.IsDismissed,
                    dismissal.IsReplay,
                    key,
                    correlation);
            }),
            cancellationToken);
    }

    private static MailAction ParseMailAction(string? action) => action?.Trim() switch
    {
        "move_folder" => MailAction.MoveFolder,
        "dismiss" => MailAction.Dismiss,
        "restore" => MailAction.Restore,
        _ => throw new McpException("action must be move_folder, dismiss or restore.")
    };

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
        summary.CaseReference,
        summary.IsTriageCase);

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
        MailClassificationSelection.OptionsFor(dossier.MessageDirection));

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
        result.PolicyVersion,
        result.CaseType?.ToString());
}
