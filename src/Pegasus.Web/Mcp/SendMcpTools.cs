using System.ComponentModel;
using System.Net.Mail;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Pages.Mail;

namespace Pegasus.Web.Mcp;

internal sealed record ReportSendToolResult(
    [property: Description("The Case the report was sent for.")] Guid CaseId,
    [property: Description("current or inspection: the work whose report was sent.")] string Work,
    [property: Description("The report generation sent.")] Guid GenerationId,
    [property: Description("The send operation; replaying the same operation key returns it.")] Guid OperationId,
    [property: Description("The send state: Submitted once the provider accepted it, Sent once the Sent item is observed, Failed with failureCode, Unknown when the provider outcome is not known (never resend; replay the key), or an in-flight state.")] string State,
    [property: Description("Why a Failed or Unknown send stopped, as recorded.")] string? FailureCode,
    [property: Description("The Case version after the send.")] long CaseVersion,
    [property: Description("The Case's lifecycle state after the send.")] string CaseState,
    string OperationKey,
    string CorrelationId);

internal sealed record MailSendToolResult(
    [property: Description("The send operation; replaying the same operation key with the same content returns it.")] Guid OperationId,
    [property: Description("new, reply, reply_all, forward or triage_reply.")] string Mode,
    [property: Description("What the send is recorded as: GeneralCorrespondence, CaseChaser, TriageChaser or TriageOutcomeReply.")] string Purpose,
    [property: Description("The send state: Submitted once the provider accepted it, Sent once the Sent item is observed, Failed with failureCode, Unknown when the provider outcome is not known (never resend; replay the key), or an in-flight state.")] string State,
    [property: Description("Why a Failed or Unknown send stopped, as recorded.")] string? FailureCode,
    [property: Description("The Case the mail is filed against (the Triage Case for triage_reply).")] Guid CaseId,
    [property: Description("The retained message answered or forwarded; null for new.")] Guid? MessageId,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Outward sending for the Automation Actor (ADR-0064 phase 4, operator
/// 7 October 2026), behind its own <c>automation.send</c> scope so a grant can
/// hold casework without sending. Each tool calls the command the staff page
/// calls: the report send of the Case page's Send report
/// (<see cref="ISendCaseReport"/>), and the staff mail send of the Inbox
/// composer, the message page's Reply, Reply all and Forward, and the Triage
/// Case's reply (<see cref="IStaffMailSend"/>). Mail leaves the same approved
/// mailbox a staff send would, and the send is recorded as the Automation
/// Actor's.
/// </summary>
[McpServerToolType]
internal sealed class SendMcpTools(
    ISendCaseReport sendCaseReport,
    ICaseReportGenerationStore reportGenerations,
    IGetAssessmentAccess getAssessmentAccess,
    IGetCaseHeader getCaseHeader,
    ISearchCases searchCases,
    ICaseWorkflowQueries workflowQueries,
    IStaffMailSend staffMailSend,
    IStaffMailAttachmentResolver attachmentResolver,
    IApprovedMailboxStore approvedMailboxes,
    GetRetainedMail getRetainedMail,
    IGetTriage getTriage,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    private static readonly string[] Modes = ["new", "reply", "reply_all", "forward", "triage_reply"];

    [McpServerTool(
        Name = "pegasus_report_send",
        Title = "Send case report",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Sends a Case's generated report to the Principal, as Send report on the Case page does, through the same Core command: the work's current confirmed generation (or generationId), the reviewed To and Cc recipients (omit both for the Principal's suggested recipients), the companion documents chosen (omit attach for every confirmed document the generation holds, the fee note included) and the covering message, which is sent as written. It leaves the approved mailbox bound for staff send and report-Sent evidence; the report attaches under its delivery name. The Case must be open in a state its report can be sent in; a Complete Case sends only the Inspection report of a Case that has its Audit. Needs automation.send and the expected case version (present an edit lease token, or omit it and the tool holds the lease for this one send). The result is the send's state: Submitted is accepted, not yet Sent; Unknown must never be resent, replay the same operation key instead.")]
    public async Task<ReportSendToolResult> SendReportAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same send without sending again.")] string operationKey,
        [Description("The covering message sent with the report, as written (plain text, line breaks kept).")] string coveringMessage,
        [Description("current (the default; the Audit once a Case has one) or inspection (the Inspection report of a Case that has its Audit).")] string? work = null,
        [Description("The confirmed generation to send (pegasus_report_list); omit for the work's current generation. Only the work's current generation is sendable.")] Guid? generationId = null,
        [Description("The generation version the caller observed; omit to send the generation as it stands now.")] long? expectedGenerationVersion = null,
        [Description("To recipients, plain e-mail addresses; omit both to and cc for the Principal's suggested recipients.")] string[]? to = null,
        [Description("Cc recipients, plain e-mail addresses. An address also in To is sent once, in To.")] string[]? cc = null,
        [Description("The companion documents to attach beside the report: FeeNote, RepairSpecification and/or ImagePack. Each must be confirmed on the generation. Omit for every confirmed document the generation holds.")] string[]? attach = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one send.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.SendScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_report_send",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var actor = context.Actor;
                var selector = ParseWork(work);
                var kinds = ParseAttach(attach);
                ReportRecipientReview? review = to is null && cc is null
                    ? null
                    : new(
                        Recipients(to, "To").Select(recipient => recipient.Address).ToArray(),
                        Recipients(cc, "Cc").Select(recipient => recipient.Address).ToArray());
                await RequireReportSendableAsync(actor, caseId, selector, cancellationToken);
                var requested = generationId is { } named
                    ? AutomationMcpErrors.RequireId(named, "generation identifier")
                    : (Guid?)null;
                var generation = requested is { } wanted
                    ? (await reportGenerations.ListAsync(actor, caseId, selector, cancellationToken))
                        .FirstOrDefault(item => item.Id == wanted)
                        ?? throw new McpException("The generation is not a report generation of this work.")
                    : await reportGenerations.GetCurrentAsync(actor, caseId, selector, cancellationToken)
                        ?? throw new McpException("The work has no report generation to send; generate the report first.");

                StaffMailOperation operation;
                try
                {
                    operation = await leases.RunCaseAsync(
                        caseId,
                        expectedVersion,
                        editLeaseToken,
                        actor,
                        key,
                        token => sendCaseReport.ExecuteAsync(
                            new(
                                actor,
                                caseId,
                                expectedVersion,
                                token,
                                generation.Id,
                                expectedGenerationVersion ?? generation.Version,
                                key,
                                coveringMessage,
                                review,
                                kinds,
                                selector),
                            cancellationToken),
                        cancellationToken);
                }
                catch (KeyNotFoundException)
                {
                    throw new McpException("The case was not found.");
                }

                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new ReportSendToolResult(
                    caseId,
                    selector == CaseWorkSelector.Primary ? "inspection" : "current",
                    generation.Id,
                    operation.Id,
                    operation.State.ToString(),
                    operation.FailureCode,
                    workflow.Version,
                    workflow.State.ToString(),
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_mail_send",
        Title = "Send e-mail",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Sends e-mail as staff do, through the same Core staff mail send, filed against a Case. mode new is the Inbox composer: new correspondence from the default approved mailbox to the To and Cc given (chaser true records it as the Case's chaser, as Send chaser does). reply, reply_all and forward answer one retained message (messageId from pegasus_mail_list) from the approved mailbox that holds it: reply goes to its Reply-To or sender and reply_all adds its other recipients, so neither takes to or cc; forward takes to and cc. triage_reply is the Triage Case's reply to the e-mail the Triage was opened from: the chaser until the outcome is recorded, the outcome reply once Completed, none once Cancelled; To defaults to the original sender. Subject and body are sent as written. Attachments are Case files by documentVersionId (pegasus_case_get documents), or for triage_reply the origin item's files by assetId (pegasus_intake_get). Needs automation.send, the expected version and a mcp:-prefixed operation key; replaying the key with the same content returns the same send. The result is the send's state: Submitted is accepted, not yet Sent; Unknown must never be resent.")]
    public async Task<MailSendToolResult> SendMailAsync(
        [Description("new, reply, reply_all, forward or triage_reply.")] string mode,
        [Description("The Case the mail is filed against: an instruction Case (pegasus_case_search) for new, reply, reply_all and forward; the Triage Case (pegasus_triage_list) for triage_reply.")] Guid caseId,
        [Description("The version the caller observed: the Case's version for new, reply, reply_all and forward; the Triage version for triage_reply. A stale value fails closed.")] long expectedVersion,
        [Description("The subject, sent as written.")] string subject,
        [Description("The message body, plain text, sent as written.")] string body,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("reply, reply_all and forward: the retained message answered or forwarded (pegasus_mail_list).")] Guid? messageId = null,
        [Description("To recipients, plain e-mail addresses: required for new and forward; triage_reply defaults to the original sender; reply and reply_all take none.")] string[]? to = null,
        [Description("Cc recipients, plain e-mail addresses: new, forward and triage_reply only.")] string[]? cc = null,
        [Description("new only: send it as the Case's chaser, recorded as a chase once it is Sent.")] bool chaser = false,
        [Description("Files to attach: Case document version ids (documentVersionId from pegasus_case_get), or for triage_reply the origin item's assetIds (pegasus_intake_get).")] Guid[]? attachments = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.SendScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_mail_send",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var verb = mode?.Trim() ?? string.Empty;
                if (!Modes.Contains(verb, StringComparer.Ordinal))
                {
                    throw new McpException("The mode must be one of: " + string.Join(", ", Modes) + ".");
                }
                if (string.IsNullOrWhiteSpace(subject))
                {
                    throw new McpException("A subject is required.");
                }
                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new McpException("A message body is required.");
                }
                if (chaser && verb != "new")
                {
                    throw new McpException("chaser applies to new mail only.");
                }
                if (messageId is not null && verb is "new" or "triage_reply")
                {
                    throw new McpException($"{verb} takes no messageId.");
                }

                var (command, message) = verb switch
                {
                    "new" => (await NewMailAsync(
                        context.Actor, caseId, expectedVersion, to, cc, chaser, attachments, cancellationToken), (Guid?)null),
                    "triage_reply" => await TriageReplyAsync(
                        context.Actor, caseId, expectedVersion, to, cc, attachments, cancellationToken),
                    _ => await RetainedReplyAsync(
                        context.Actor, verb, caseId, expectedVersion, messageId, to, cc, attachments, cancellationToken),
                };

                var operation = command is null
                    // The outcome reply for this completion is already Sent.
                    ? message is { } answered
                        ? await staffMailSend.GetLatestForOriginalAsync(context.Actor, answered, cancellationToken)
                            ?? throw new McpException("The outcome reply has already been sent.")
                        : throw new McpException("The outcome reply has already been sent.")
                    : await staffMailSend.SendAsync(
                        command with
                        {
                            Subject = subject.Trim(),
                            Body = body.Trim(),
                            OperationKey = key,
                        },
                        cancellationToken);
                return new MailSendToolResult(
                    operation.Id,
                    verb,
                    operation.Purpose.ToString(),
                    operation.State.ToString(),
                    operation.FailureCode,
                    caseId,
                    message,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    /// <summary>The Inbox composer's send: new correspondence, or a Case chaser.</summary>
    private async Task<StaffMailSendCommand> NewMailAsync(
        ActionActor actor,
        Guid caseId,
        long expectedVersion,
        string[]? to,
        string[]? cc,
        bool chaser,
        Guid[]? attachments,
        CancellationToken cancellationToken)
    {
        var recipients = Recipients(to, "To");
        if (recipients.Length == 0)
        {
            throw new McpException("At least one To recipient is required.");
        }
        var mailbox = ComposeModel.DefaultSender(await approvedMailboxes.ListAsync(cancellationToken))
            ?? throw new McpException("No default approved mailbox is configured for correspondence.");
        var header = await RequireCorrespondenceCaseAsync(actor, caseId, expectedVersion, cancellationToken);
        return new(
            actor,
            mailbox.Id,
            mailbox.Generation,
            chaser ? StaffMailPurpose.CaseChaser : StaffMailPurpose.GeneralCorrespondence,
            header.Summary.CaseId,
            header.Workflow.Version,
            StaffMailComposeMode.New,
            OriginalMessage: null,
            recipients,
            Recipients(cc, "Cc"),
            Subject: string.Empty,
            Body: string.Empty,
            await CaseAttachmentsAsync(actor, header.Summary.CaseId, attachments, cancellationToken),
            OperationKey: string.Empty);
    }

    /// <summary>The message page's Reply, Reply all or Forward of one retained message.</summary>
    private async Task<(StaffMailSendCommand? Command, Guid? MessageId)> RetainedReplyAsync(
        ActionActor actor,
        string verb,
        Guid caseId,
        long expectedVersion,
        Guid? messageId,
        string[]? to,
        string[]? cc,
        Guid[]? attachments,
        CancellationToken cancellationToken)
    {
        var id = messageId is { } named
            ? AutomationMcpErrors.RequireId(named, "retained message identifier")
            : throw new McpException($"{verb} needs messageId.");
        var mode = verb switch
        {
            "reply" => StaffMailComposeMode.Reply,
            "reply_all" => StaffMailComposeMode.ReplyAll,
            _ => StaffMailComposeMode.Forward,
        };
        if (mode != StaffMailComposeMode.Forward && (to is not null || cc is not null))
        {
            throw new McpException($"{verb} goes to the message's own recipients and takes no to or cc.");
        }

        var detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken)
            ?? throw new McpException("The retained message was not found.");
        var mailbox = MessageModel.CorrespondenceSender(detail, await approvedMailboxes.ListAsync(cancellationToken))
            ?? throw new McpException("Correspondence is unavailable for this message: no send-ready approved mailbox holds it.");
        var (toRecipients, ccRecipients) = mode switch
        {
            StaffMailComposeMode.Reply => MessageModel.ReplyRecipients(detail),
            StaffMailComposeMode.ReplyAll => MessageModel.ReplyAllRecipients(detail, mailbox.Address),
            _ => (Recipients(to, "To"), Recipients(cc, "Cc")),
        };
        if (toRecipients.Length == 0)
        {
            throw new McpException(mode == StaffMailComposeMode.Forward
                ? "At least one To recipient is required."
                : "The message has no address to reply to.");
        }

        var header = await RequireCorrespondenceCaseAsync(actor, caseId, expectedVersion, cancellationToken);
        return (
            new(
                actor,
                mailbox.Id,
                mailbox.Generation,
                StaffMailPurpose.GeneralCorrespondence,
                header.Summary.CaseId,
                header.Workflow.Version,
                mode,
                new(
                    detail.Summary.Id,
                    detail.Summary.MailboxId,
                    detail.ImmutableMessageId,
                    detail.InternetMessageId,
                    detail.ConversationId),
                toRecipients,
                ccRecipients,
                Subject: string.Empty,
                Body: string.Empty,
                await CaseAttachmentsAsync(actor, header.Summary.CaseId, attachments, cancellationToken),
                OperationKey: string.Empty),
            detail.Summary.Id);
    }

    /// <summary>
    /// The Triage Case's reply to its origin e-mail: the chaser until the
    /// outcome is recorded, Reply with outcome once Completed. A command of
    /// null means the outcome reply of the current completion is already Sent,
    /// which a repeated call reads back rather than sending twice.
    /// </summary>
    private async Task<(StaffMailSendCommand? Command, Guid? MessageId)> TriageReplyAsync(
        ActionActor actor,
        Guid caseId,
        long expectedVersion,
        string[]? to,
        string[]? cc,
        Guid[]? attachments,
        CancellationToken cancellationToken)
    {
        var triage = await getTriage.ExecuteAsync(new(caseId, actor), cancellationToken)
            ?? throw new McpException("The Triage record was not found.");
        if (triage.Record.Origin is not { } origin
            || origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            throw new McpException("A Triage reply answers the e-mail the Triage was opened from; this Triage has none.");
        }
        if (TriageLifecycleRules.ReplyPurpose(triage.Record.State) is not { } purpose)
        {
            throw new McpException("A cancelled Triage takes no reply.");
        }

        var detail = await getRetainedMail.ExecuteByOriginReceiptAsync(actor, origin.ReceiptId, cancellationToken)
            ?? throw new McpException("The Triage's originating retained message was not found.");
        if (TriageLifecycleRules.OutcomeReplySent(triage))
        {
            return (null, detail.Summary.Id);
        }
        if (triage.Record.Version != expectedVersion)
        {
            throw new McpException(
                $"Refused: the Triage changed since it was read. It is at version {triage.Record.Version}, not {expectedVersion}; reload it with pegasus_triage_get.");
        }

        var mailbox = DetailsModel.TriageReplySender(
                await approvedMailboxes.ListAsync(cancellationToken),
                detail.Summary.MailboxId)
            ?? throw new McpException("No approved mailbox with staff send capability is available for this origin.");
        var toRecipients = Recipients(to, "To");
        if (toRecipients.Length == 0)
        {
            toRecipients = DetailsModel.TriageReplyRecipients(detail);
        }
        if (toRecipients.Length == 0)
        {
            throw new McpException("At least one To recipient is required.");
        }

        var options = await attachmentResolver.ListIntakeAsync(actor, origin.ReceiptId, cancellationToken);
        var resolved = await attachmentResolver.ResolveIntakeAsync(
            actor, origin.ReceiptId, Selections(options, attachments, "Triage's origin item"), cancellationToken);
        return (
            new(
                actor,
                mailbox.Id,
                mailbox.Generation,
                purpose,
                triage.Record.CaseId,
                triage.Record.Version,
                StaffMailComposeMode.Reply,
                new(
                    detail.Summary.Id,
                    detail.Summary.MailboxId,
                    detail.ImmutableMessageId,
                    detail.InternetMessageId,
                    detail.ConversationId),
                toRecipients,
                Recipients(cc, "Cc"),
                Subject: string.Empty,
                Body: string.Empty,
                resolved,
                OperationKey: string.Empty),
            detail.Summary.Id);
    }

    /// <summary>
    /// The instruction Case correspondence is filed against, found as the
    /// composer finds it: by its Case/PO reference, which a Triage Case's
    /// t. reference never resolves. The version the caller saw must stand.
    /// </summary>
    private async Task<CaseHeader> RequireCorrespondenceCaseAsync(
        ActionActor actor,
        Guid caseId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var header = await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken)
            ?? throw new McpException("The case was not found.");
        var matches = await searchCases.ExecuteAsync(
            new(actor, new CaseSearchFilters(CaseReference: header.Summary.Reference), PageSize: 2),
            cancellationToken);
        if (!matches.Items.Any(item => item.CaseId == header.Summary.CaseId))
        {
            throw new McpException(
                "Correspondence is filed against an instruction Case; a Triage Case is answered with triage_reply.");
        }
        if (header.Workflow.Version != expectedVersion)
        {
            throw new McpException(
                $"Refused: the case changed since it was read. The case is at version {header.Workflow.Version}, not {expectedVersion}; reload it and retry.");
        }
        return header;
    }

    private async Task<IReadOnlyList<StaffMailAttachment>> CaseAttachmentsAsync(
        ActionActor actor,
        Guid caseId,
        Guid[]? attachments,
        CancellationToken cancellationToken)
    {
        if (attachments is not { Length: > 0 })
        {
            return [];
        }
        var options = await attachmentResolver.ListCaseAsync(actor, caseId, cancellationToken);
        return await attachmentResolver.ResolveCaseAsync(
            actor, caseId, Selections(options, attachments, "Case"), cancellationToken);
    }

    /// <summary>The composer's selections for the files the caller named by id.</summary>
    private static string[] Selections(
        IReadOnlyList<StaffMailAttachmentOption> options,
        Guid[]? ids,
        string source) =>
        (ids ?? [])
            .Select(id => options.FirstOrDefault(option => id != Guid.Empty && option.SourceId == id)?.Selection
                ?? throw new McpException($"Attachment {id:D} is not a sendable file of this {source}."))
            .ToArray();

    /// <summary>
    /// The Case page's Send report gate: an open assessment, not read-only
    /// unless it is the Inspection report of a Case that has its Audit.
    /// </summary>
    private async Task RequireReportSendableAsync(
        ActionActor actor,
        Guid caseId,
        CaseWorkSelector selector,
        CancellationToken cancellationToken)
    {
        var access = await getAssessmentAccess.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (access is null || !access.CanOpen)
        {
            throw new McpException("The case was not found or its report cannot be sent in its state.");
        }
        if (access.IsReadOnly
            && !(selector == CaseWorkSelector.Primary
                && (await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken))?.Works is { HasAudit: true }))
        {
            throw new McpException("The case is read-only once Complete.");
        }
    }

    private static StaffMailRecipient[] Recipients(string[]? values, string field) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => MailAddress.TryCreate(value, out var parsed)
                && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase)
                    ? new StaffMailRecipient(value, DisplayName: null)
                    : throw new McpException($"Each {field} recipient must be a plain e-mail address."))
            .DistinctBy(recipient => recipient.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static CaseReportArtifactKind[]? ParseAttach(string[]? attach) =>
        attach is null
            ? null
            : attach
                .Select(value => value is not null
                    && Enum.GetNames<CaseReportArtifactKind>().Contains(value.Trim(), StringComparer.Ordinal)
                        ? Enum.Parse<CaseReportArtifactKind>(value.Trim())
                        : throw new McpException("attach takes FeeNote, RepairSpecification and ImagePack."))
                .Distinct()
                .ToArray();

    private static CaseWorkSelector ParseWork(string? work) => work?.Trim() switch
    {
        null or "" or "current" => CaseWorkSelector.Current,
        "inspection" => CaseWorkSelector.Primary,
        _ => throw new McpException("The work must be current or inspection."),
    };
}
