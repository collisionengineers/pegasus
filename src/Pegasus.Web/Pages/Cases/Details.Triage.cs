using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.Triage;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What a Triage Case's record renders on <c>/Cases/{id}</c>: the ribbon, the
/// Determinations panel, the evidence images, the exact response evidence,
/// the reply the correspondence panel offers, the Case Files panel and the
/// Triage notes. There is no Edit step: every action claims and releases the
/// Triage edit scope inside its own save (<see cref="TriageWriteAuthority"/>).
/// </summary>
public sealed class TriageCaseView(TriageDetail triage)
{
    public TriageDetail Triage { get; } = triage ?? throw new ArgumentNullException(nameof(triage));

    public TriageRecord Record => Triage.Record;

    /// <summary>The Triage Case's <c>t.</c> Case/PO.</summary>
    public string Reference => Triage.Record.Reference;

    /// <summary>Each evidence image's recorded crop and tags, by its asset (pre-Case crop and tag, v26).</summary>
    public IReadOnlyDictionary<Guid, Pegasus.Core.ImageIntake.PreCaseImagePreparation> Preparations { get; set; } =
        new Dictionary<Guid, Pegasus.Core.ImageIntake.PreCaseImagePreparation>();

    /// <summary>The tag vocabulary the viewer's Tag select offers.</summary>
    public IReadOnlyList<Pegasus.Core.Documents.ImageTag> ImageTags { get; set; } = [];

    public IReadOnlyList<TriageFinding> ActiveFindings { get; set; } = [];

    /// <summary>The finding the record stands on, when it has exactly one.</summary>
    public TriageFinding? CurrentFinding => ActiveFindings.Count == 1 ? ActiveFindings[0] : null;

    public RetainedMailDetail? RetainedMail { get; set; }

    public StaffMailOperation? ReplyOperation { get; set; }

    public bool ReplyOperationBlocked { get; set; }

    public ApprovedMailbox? ReplyMailbox { get; set; }

    public string ReplyOperationKey { get; set; } = string.Empty;

    public string? ReplyTo { get; set; }

    public string? ReplyCc { get; set; }

    public string? ReplySubject { get; set; }

    public string? ReplyBody { get; set; }

    /// <summary>The reply this state offers: the chaser, Reply with outcome, or none once Cancelled.</summary>
    public StaffMailPurpose? ReplyPurpose => TriageLifecycleRules.ReplyPurpose(Record.State);

    public bool IsOutcomeReply => ReplyPurpose == StaffMailPurpose.TriageOutcomeReply;

    /// <summary>A reply can be sent: the Triage came by e-mail and its approved mailbox may send.</summary>
    public bool CanSendReply => RetainedMail is not null && ReplyMailbox is not null;

    /// <summary>
    /// Reply with outcome is offered: the Triage is Completed, a reply can be
    /// sent and no send is in flight.
    /// </summary>
    public bool OffersReply => IsOutcomeReply && CanSendReply && !ReplyOperationBlocked;

    /// <summary>The completion notice carries a Reply with outcome link when a reply can be sent.</summary>
    public bool NoticeOffersReply { get; set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; set; } = [];

    public IReadOnlyList<string> SelectedAttachments { get; set; } = [];

    /// <summary>
    /// The photographs the provider attached to the Triage request: the origin
    /// receipt's own retained assets. A Triage Case staff created directly has
    /// no origin receipt, so none.
    /// </summary>
    public IReadOnlyList<IntakeAssetRecord> EvidenceImages { get; set; } = [];

    /// <summary>
    /// What the link to the origin receipt opens, named for the material
    /// itself: an e-mail for mailbox material, a file otherwise.
    /// </summary>
    public bool SourceIsEmail { get; set; }

    /// <summary>The retained message the request came in, so the record offers Open message (never a receipt page).</summary>
    public Guid? SourceMessageId => RetainedMail?.Summary.Id;

    /// <summary>Open file: the retained original through the kept source route; none without an origin receipt.</summary>
    public string? OpenFileHref => Record.Origin is { } origin
        ? $"/Received/{origin.ReceiptId:D}/Source"
        : null;

    public string? AssigneeName { get; set; }

    /// <summary>The linked instruction Case's Case/PO, when it can be read.</summary>
    public string? LinkedCaseReference { get; set; }

    public string? CaseAssociationUnavailableReason { get; set; }

    public Guid? CaseAssociationUnavailableCaseId { get; set; }

    public string OperationKey { get; set; } = StaffPageModel.NewOperationKey();

    public string? Message { get; set; }

    /// <summary>The enabled staff accounts this Triage may be assigned to, the signed-in account first.</summary>
    public IReadOnlyList<CaseEngineerChoice> EngineerChoices { get; set; } = [];

    /// <summary>The signed-in staff account, marked "(you)" in the roster.</summary>
    public Guid ViewerStaffId { get; set; }

    public string AssigneeChoiceLabel(CaseEngineerChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return choice.StaffId == ViewerStaffId ? Labels.You(choice.DisplayName) : choice.DisplayName;
    }

    /// <summary>The Triage Case's files: its current documents, as the Case Files section lists them.</summary>
    public IReadOnlyList<Pegasus.Core.Documents.CaseFile> Files =>
        [.. Pegasus.Core.Documents.CaseFiles.Current(Triage.Documents)];

    /// <summary>The audited download of one of the Triage Case's files.</summary>
    public string DownloadUrl(Pegasus.Core.Documents.CaseFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return $"/Cases/{Record.CaseId:D}/Documents/{file.Occurrence.Id:D}/Download?versionId={file.Version.Id:D}";
    }

    /// <summary>
    /// Where the request came from; a Triage Case staff created directly has
    /// no route evidence and reads as a manual arrival.
    /// </summary>
    public string SourceLabel => Record.Origin is { } origin
        ? SourceChannelLabel(origin.SourceIdentity.Channel)
        : OperatorLabels.WorkCentre.Arrival(CaseArrival.Manual);

    public static string StateLabel(TriageState state) => OperatorLabels.TriageState(state);

    public static string SourceChannelLabel(IntakeSourceChannel channel) =>
        OperatorLabels.SourceChannel(channel);

    public static string RoadworthinessLabel(RoadworthinessFinding finding) => TriageOutcomeReply.Label(finding);

    public static string AssessmentLabel(AssessmentFinding finding) => TriageOutcomeReply.Label(finding);

    public static string EventLabel(string eventType) => eventType switch
    {
        "triage_created" => "Triage created",
        "triage_assigned" => "Assigned",
        "triage_unassigned" => "Unassigned",
        "triage_state_awaiting_information" => "Awaiting information",
        "triage_finding_recorded" => "Finding recorded",
        "triage_finding_superseded" => "Finding superseded",
        "sent_email_evidence_recorded" => "Sent evidence recorded",
        "email_response_evidence_recorded" => "Response evidence recorded",
        "triage_response_linked" => "Response evidence linked",
        "triage_response_unlinked" => "Response evidence unlinked",
        "triage_state_completed" => "Completed",
        "triage_state_cancelled" => "Cancelled",
        "triage_state_open" => "Reopened",
        "triage_case_linked" => "Case linked",
        "triage_case_unlinked" => "Case unlinked",
        _ => eventType
    };
}

/// <summary>
/// The Case record's Triage Case members. A Triage Case is a Case with its own
/// lifecycle and no Case workflow, so <c>/Cases/{id}</c> dispatches on the
/// Case's kind: a Triage Case renders its Triage workspace and answers only the
/// <c>Triage*</c> handlers; every other Case answers only the Case handlers.
/// </summary>
public sealed partial class DetailsModel
{
    private const string TriageHandlerPrefix = "Triage";

    /// <summary>The Triage Case being rendered, when the Case is one.</summary>
    public TriageCaseView? TriageCase { get; private set; }

    /// <summary>
    /// A <c>Triage*</c> handler answers only on a Triage Case and every other
    /// handler (the section fragment included) only on a Case that is not
    /// one; the wrong kind is not found. The record's own read dispatches in
    /// <see cref="OnGetAsync"/>.
    /// </summary>
    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var handlerName = context.HandlerMethod?.Name;
        if (!string.IsNullOrEmpty(handlerName)
            && context.RouteData.Values.TryGetValue("id", out var routeId)
            && Guid.TryParse(Convert.ToString(routeId, System.Globalization.CultureInfo.InvariantCulture), out var caseId))
        {
            var kind = await getCaseKind.ExecuteAsync(caseId, context.HttpContext.RequestAborted);
            var isTriageHandler = handlerName.StartsWith(TriageHandlerPrefix, StringComparison.Ordinal);
            if ((kind == CaseType.Triage) != isTriageHandler)
            {
                context.Result = NotFound();
                return;
            }
        }

        await next();
    }

    /// <summary>The Triage Case's own read on <c>/Cases/{id}</c>.</summary>
    private async Task<IActionResult> GetTriageCaseAsync(
        Guid id,
        ActionActor actor,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        if (!await LoadTriageCaseAsync(id, actor, ports, cancellationToken))
        {
            return NotFound();
        }

        var view = TriageCase!;
        view.Message = TempData["TriageStatus"] as string;
        if (TempData["TriageUnavailableCase"] is string unavailableCase)
        {
            var separator = unavailableCase.IndexOf('|', StringComparison.Ordinal);
            if (separator > 0
                && Guid.TryParse(unavailableCase.AsSpan(0, separator), out var parsedCaseId)
                && parsedCaseId != Guid.Empty)
            {
                view.CaseAssociationUnavailableCaseId = parsedCaseId;
                view.CaseAssociationUnavailableReason = unavailableCase[(separator + 1)..];
            }
        }

        return Page();
    }

    /// <summary>
    /// One staff action on the Triage. Each posts once: the action claims the
    /// Triage edit scope for this save, runs, and the store ends the scope as
    /// the save commits. Complete and Await information carry no reason.
    /// </summary>
    public async Task<IActionResult> OnPostTriageActionAsync(
        Guid id,
        string actionName,
        long expectedVersion,
        string operationKey,
        string? reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        Guid? supersedesFindingId,
        string? responseCandidate,
        Guid? sentEvidenceId,
        Guid? caseId,
        Guid? assigneeId,
        string? note,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actionActor))
        {
            return Forbid();
        }

        if (actionName is "link_case" or "unlink_case")
        {
            return await ExecuteTriageCaseAssociationAsync(
                linking: actionName == "link_case",
                id,
                caseId ?? Guid.Empty,
                expectedVersion,
                actionActor,
                operationKey,
                reason ?? string.Empty,
                ports,
                cancellationToken);
        }

        // The assignee is chosen from the eligible roster, never defaulted to
        // whoever is signed in; a choice outside the roster is no choice.
        CaseEngineerChoice? assignee = null;
        if (actionName == "assign")
        {
            var roster = await ports.EngineerChoices.GetAsync(actionActor, cancellationToken);
            assignee = roster.FirstOrDefault(choice => choice.StaffId == assigneeId);
            if (assignee is null)
            {
                ModelState.AddModelError("assigneeId", Labels.ChooseAssignee);
                return await GetTriageCaseAsync(id, actionActor, ports, cancellationToken);
            }
        }

        string message;
        var nextOperationKey = operationKey;
        var applied = false;
        Task<string> RunAsync(string editLeaseToken) => ExecuteTriageActionAsync(
            actionName,
            id,
            expectedVersion,
            actionActor,
            operationKey,
            editLeaseToken,
            reason ?? string.Empty,
            roadworthiness,
            assessment,
            supersedesFindingId,
            responseCandidate,
            sentEvidenceId,
            assignee,
            note,
            ports,
            cancellationToken);
        try
        {
            try
            {
                message = await TriageWriteAuthority.ExecuteAsync(
                    ports.EditScopes,
                    id,
                    expectedVersion,
                    actionActor,
                    operationKey,
                    logger,
                    RunAsync,
                    cancellationToken);
            }
            catch (EditScopeVersionConflictException)
            {
                // The record has moved past the posted version. A repeat of a
                // committed post (a double click, a reload that re-posts) is
                // answered from its operation key before Core checks the hold,
                // so it gets the first result and its notice. A genuinely
                // stale post fails the store's version check and is refused
                // as changed below.
                message = await RunAsync(string.Empty);
            }

            applied = true;
            nextOperationKey = NewOperationKey();
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (EditScopeConflictException)
        {
            message = await TriageWriteAuthority.DescribeHeldAsync(
                ports.EditScopes,
                ports.DescribeEditAuthorityHolder,
                id,
                actionActor,
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            message = Labels.Expired;
        }
        catch (Exception exception)
            when (exception is EditScopeVersionConflictException or TriageVersionConflictException)
        {
            message = Labels.Changed;
        }
        catch (Exception exception) when (IsExpectedTriageRefusal(exception))
        {
            message = exception.Message;
        }

        if (!await LoadTriageCaseAsync(id, actionActor, ports, cancellationToken))
        {
            return NotFound();
        }

        TriageCase!.Message = message;
        TriageCase.OperationKey = nextOperationKey;
        TriageCase.NoticeOffersReply = applied && actionName == "complete" && TriageCase.OffersReply;
        return Page();
    }

    /// <summary>Runs the named action with the scope token and returns its notice.</summary>
    private static async Task<string> ExecuteTriageActionAsync(
        string actionName,
        Guid id,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string editLeaseToken,
        string reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        Guid? supersedesFindingId,
        string? responseCandidate,
        Guid? sentEvidenceId,
        CaseEngineerChoice? assignee,
        string? note,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        var mutation = new TriageMutationRequest(id, expectedVersion, actor, operationKey, reason)
        {
            EditLeaseToken = editLeaseToken
        };
        var transition = new TriageTransitionRequest(id, expectedVersion, actor, operationKey)
        {
            EditLeaseToken = editLeaseToken
        };
        switch (actionName)
        {
            case "assign":
                await ports.Assign.ExecuteAsync(
                    new(id, expectedVersion, assignee!.StaffId, actor, operationKey, string.Empty)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.AssignedTo(assignee.DisplayName);
            case "unassign":
                await ports.Unassign.ExecuteAsync(transition, cancellationToken);
                return Labels.Unassigned;
            case "note":
                await ports.AddNote.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, note ?? string.Empty)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.NoteAdded;
            case "await_information":
                await ports.AwaitInformation.ExecuteAsync(transition, cancellationToken);
                return Labels.AwaitingInformation;
            case "record_finding":
                await ports.RecordFinding.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, reason, roadworthiness, assessment, null)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.FindingRecorded;
            case "supersede_finding":
                await ports.SupersedeFinding.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, reason, roadworthiness, assessment, supersedesFindingId)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.FindingRecorded;
            case "link_response":
            {
                var candidate = ParseTriageResponseCandidate(responseCandidate);
                await ports.LinkResponseEvidence.ExecuteAsync(
                    new(id, candidate.PollOutcomeId, candidate.SentEvidenceId, expectedVersion, actor, operationKey, reason)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.ResponseLinked;
            }
            case "unlink_response":
                await ports.UnlinkResponseEvidence.ExecuteAsync(
                    new(id, sentEvidenceId ?? Guid.Empty, expectedVersion, actor, operationKey, reason)
                    {
                        EditLeaseToken = editLeaseToken
                    },
                    cancellationToken);
                return Labels.ResponseUnlinked;
            case "complete":
                await ports.Complete.ExecuteAsync(transition, cancellationToken);
                return Labels.Completed;
            case "cancel":
                await ports.Cancel.ExecuteAsync(mutation, cancellationToken);
                return Labels.Cancelled;
            case "reopen":
                await ports.Reopen.ExecuteAsync(mutation, cancellationToken);
                return Labels.Reopened;
            default:
                throw new ArgumentException("The requested Triage action is not supported.");
        }
    }

    /// <summary>
    /// The one reply form: the chaser until the outcome is recorded, Reply with
    /// outcome once Completed. The purpose is the state's, never the form's.
    /// </summary>
    public async Task<IActionResult> OnPostTriageSendReplyAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? to,
        string? cc,
        string? subject,
        string? body,
        List<string>? selectedAttachments,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actionActor))
        {
            return Forbid();
        }

        try
        {
            StaffAuthorization.Require(actionActor, StaffAccessRight.PerformCasework);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        if (ports.RetainedMail is null || ports.StaffMailSend is null || ports.ApprovedMailboxes is null
            || ports.AttachmentResolver is null)
        {
            return NotFound();
        }

        var triage = await ports.GetTriage.ExecuteAsync(new(id, actionActor), cancellationToken);
        if (triage is null)
        {
            return NotFound();
        }

        // A refused send redisplays what the operator typed, never the
        // template again.
        var draft = new TriageReplyDraft(to, cc, subject, body, selectedAttachments ?? []);

        // A reply answers the mailbox request a Triage Case was opened from.
        if (triage.Record.Origin is not { } origin
            || origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            ModelState.AddModelError(string.Empty, Labels.ReplyNeedsEmail);
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        if (TriageLifecycleRules.ReplyPurpose(triage.Record.State) is not { } purpose)
        {
            ModelState.AddModelError(string.Empty, Labels.NoReplyWhenCancelled);
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        if (triage.Record.Version != expectedVersion)
        {
            ModelState.AddModelError(
                string.Empty,
                "The triage record changed while this was being prepared. Reload the record and try again.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        var detail = await ports.RetainedMail.ExecuteByOriginReceiptAsync(
            actionActor,
            origin.ReceiptId,
            cancellationToken);
        if (detail is null)
        {
            ModelState.AddModelError(string.Empty, "Originating retained message was not found.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        var mailboxes = await ports.ApprovedMailboxes.ListAsync(cancellationToken);
        var mailbox = mailboxes.SingleOrDefault(item =>
            item.Id == detail.Summary.MailboxId
            && item.State == ApprovedMailboxState.Approved
            && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
            && item.Generation > 0);
        if (mailbox is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "No approved mailbox with staff send capability is available for this origin.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        if (!IsRetainedTriageOperationKey(operationKey, detail.Summary.Id))
        {
            ModelState.AddModelError(
                nameof(operationKey),
                "The send operation key is invalid or has expired.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        var toRecipients = ParseTriageRecipients(to);
        if (toRecipients.Length == 0)
        {
            toRecipients = TriageReplyRecipients(detail);
        }
        var ccRecipients = ParseTriageRecipients(cc);

        if (toRecipients.Length == 0)
        {
            ModelState.AddModelError(nameof(to), "At least one recipient is required.");
        }
        if (string.IsNullOrWhiteSpace(subject))
        {
            ModelState.AddModelError(nameof(subject), "A subject is required.");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            ModelState.AddModelError(nameof(body), "A message is required.");
        }

        IReadOnlyList<StaffMailAttachment> attachments = [];
        try
        {
            attachments = await ports.AttachmentResolver.ResolveIntakeAsync(
                actionActor, origin.ReceiptId, draft.Attachments,
                cancellationToken);
        }
        catch (StaffMailAttachmentSelectionException exception)
        {
            ModelState.AddModelError(nameof(selectedAttachments), exception.Message);
        }

        if (!ModelState.IsValid)
        {
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        var original = new StaffMailOriginalMessage(
            detail.Summary.Id,
            detail.Summary.MailboxId,
            detail.ImmutableMessageId,
            detail.InternetMessageId,
            detail.ConversationId);

        var command = new StaffMailSendCommand(
            Actor: actionActor,
            ApprovedMailboxId: mailbox.Id,
            ExpectedMailboxGeneration: mailbox.Generation,
            Purpose: purpose,
            ContextId: triage.Record.CaseId,
            ExpectedContextVersion: triage.Record.Version,
            ComposeMode: StaffMailComposeMode.Reply,
            OriginalMessage: original,
            To: toRecipients,
            Cc: ccRecipients,
            Subject: subject!.Trim(),
            Body: body!.Trim(),
            Attachments: attachments,
            OperationKey: operationKey.Trim());

        try
        {
            var operation = await ports.StaffMailSend.SendAsync(command, cancellationToken);
            TempData["TriageStatus"] = operation.State == StaffMailState.Sent
                ? Labels.Sent(purpose)
                : Labels.SendStatus(purpose, operation.State);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var currentOperation = await ports.StaffMailSend.GetLatestForOriginalAsync(
                actionActor,
                detail.Summary.Id,
                cancellationToken);
            if (!IsActiveTriageMailOperation(currentOperation))
            {
                throw;
            }
            ModelState.AddModelError(
                string.Empty,
                "The existing correspondence operation must finish or be resolved before another action.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, draft, cancellationToken);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>What the operator posted in the reply form, kept on a refused send's redisplay.</summary>
    private sealed record TriageReplyDraft(
        string? To,
        string? Cc,
        string? Subject,
        string? Body,
        IReadOnlyList<string> Attachments);

    public async Task<IActionResult> OnPostTriageReconcileReplyAsync(
        Guid id,
        Guid operationId,
        long expectedOperationVersion,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actionActor))
        {
            return Forbid();
        }

        try
        {
            StaffAuthorization.Require(actionActor, StaffAccessRight.PerformCasework);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        if (ports.StaffMailSend is null || ports.RetainedMail is null)
        {
            return NotFound();
        }

        var triage = await ports.GetTriage.ExecuteAsync(new(id, actionActor), cancellationToken);
        if (triage is null)
        {
            return NotFound();
        }

        if (triage.Record.Origin is not { } origin
            || origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            return NotFound();
        }

        var detail = await ports.RetainedMail.ExecuteByOriginReceiptAsync(
            actionActor,
            origin.ReceiptId,
            cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var operation = await ports.StaffMailSend.GetAsync(
            actionActor,
            operationId,
            cancellationToken);
        if (operation is null)
        {
            return NotFound();
        }

        if (operation.Purpose is not (StaffMailPurpose.TriageChaser or StaffMailPurpose.TriageOutcomeReply)
            || operation.ContextId != triage.Record.CaseId
            || operation.OriginalRetainedMessageId is null
            || operation.OriginalRetainedMessageId != detail.Summary.Id)
        {
            return NotFound();
        }

        if (operation.ExpectedContextVersion != triage.Record.Version)
        {
            ModelState.AddModelError(
                string.Empty,
                "The triage workflow was updated concurrently. Refresh and review the latest state.");
            if (!await LoadTriageCaseAsync(id, actionActor, ports, cancellationToken))
            {
                return NotFound();
            }

            TriageCase!.Message = "The triage workflow was updated concurrently. Refresh and review the latest state.";
            return Page();
        }

        try
        {
            await ports.StaffMailSend.ReconcileAsync(
                actionActor,
                operationId,
                expectedOperationVersion,
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return RedirectToPage(new { id });
    }

    /// <summary>The page again after a refused send, with the reply form holding what was posted.</summary>
    private async Task<IActionResult> ReloadTriageCaseAsync(
        Guid id,
        ActionActor actor,
        TriageCasePorts ports,
        TriageReplyDraft draft,
        CancellationToken cancellationToken)
    {
        if (!await LoadTriageCaseAsync(id, actor, ports, cancellationToken))
        {
            return NotFound();
        }

        var view = TriageCase!;
        view.ReplyTo = draft.To;
        view.ReplyCc = draft.Cc;
        view.ReplySubject = draft.Subject;
        view.ReplyBody = draft.Body;
        view.SelectedAttachments = draft.Attachments;
        return Page();
    }

    private async Task<bool> LoadTriageCaseAsync(
        Guid id,
        ActionActor actor,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var triage = await ports.GetTriage.ExecuteAsync(new(id, actor), cancellationToken);
        if (triage is null)
        {
            return false;
        }

        var view = new TriageCaseView(triage);
        TriageCase = view;
        var origin = triage.Record.Origin;
        // The request's photographs are the whole subject of the assessment.
        // No rights guard here: GetTriage above already required the same
        // PerformCasework right from the same actor, so this cannot be
        // reached without it.
        var receipt = origin is null
            ? null
            : await ports.GetIntake.ExecuteAsync(
                new(origin.ReceiptId, actor),
                cancellationToken);
        view.EvidenceImages = receipt is null
            ? []
            : InstructionEvidenceImages.Servable(receipt.AssetRecords);
        if (view.EvidenceImages.Count > 0)
        {
            view.Preparations = await ports.GetPreparations.ExecuteAsync(
                actor,
                view.EvidenceImages.Select(asset => asset.Id).ToArray(),
                cancellationToken);
            view.ImageTags = await ports.TagVocabulary.ListAsync(cancellationToken);
        }
        view.SourceIsEmail = receipt is not null && !string.IsNullOrWhiteSpace(receipt.MediaType)
            ? UnidentifiedMediaKindPolicy.Classify(
                receipt.SourceIdentity.Channel,
                receipt.MediaType) == UnidentifiedMediaKind.Email
            : origin?.SourceIdentity.Channel == IntakeSourceChannel.Mailbox;

        // One roster read serves the assignee's name and the Assign dialog,
        // with the signed-in account first.
        _ = Guid.TryParse(actor.SubjectId, out var viewerStaffId);
        var roster = await ports.EngineerChoices.GetAsync(actor, cancellationToken);
        view.ViewerStaffId = viewerStaffId;
        view.EngineerChoices = [.. roster.OrderBy(choice => choice.StaffId == viewerStaffId ? 0 : 1)];
        view.AssigneeName = triage.Record.AssigneeId is { } assigneeId
            ? roster.FirstOrDefault(choice => choice.StaffId == assigneeId)?.DisplayName ?? "Assigned"
            : null;

        view.ActiveFindings = triage.Findings
            .Where(candidate => !triage.Findings.Any(
                finding => finding.SupersedesFindingId == candidate.Id))
            .ToArray();
        if (triage.Record.LinkedInstructionCaseId is { } linkedCaseId)
        {
            var linkedCase = await ports.GetCase.ExecuteAsync(
                new(linkedCaseId, actor),
                cancellationToken);
            if (linkedCase is null)
            {
                view.CaseAssociationUnavailableReason =
                    "The linked case is unavailable. Case association is read-only.";
                view.CaseAssociationUnavailableCaseId = linkedCaseId;
            }
            else
            {
                view.LinkedCaseReference = linkedCase.Summary.Reference;
                if (linkedCase.ActiveEditLease is { } activeLease)
                {
                    view.CaseAssociationUnavailableReason = await DescribeTriageCaseHeldAsync(
                        activeLease,
                        actor,
                        ports,
                        cancellationToken);
                    view.CaseAssociationUnavailableCaseId = linkedCaseId;
                }
            }
        }

        if (origin is { SourceIdentity.Channel: IntakeSourceChannel.Mailbox }
            && ports.RetainedMail is not null
            && ports.StaffMailSend is not null
            && ports.ApprovedMailboxes is not null)
        {
            view.RetainedMail = await ports.RetainedMail.ExecuteByOriginReceiptAsync(
                actor,
                origin.ReceiptId,
                cancellationToken);
            if (view.RetainedMail is { } retainedMail)
            {
                if (ports.AttachmentResolver is not null)
                {
                    view.AvailableAttachments = await ports.AttachmentResolver.ListIntakeAsync(
                        actor, origin.ReceiptId, cancellationToken);
                }
                view.ReplyOperation = await ports.StaffMailSend.GetLatestForOriginalAsync(
                    actor,
                    retainedMail.Summary.Id,
                    cancellationToken);
                view.ReplyOperationBlocked = IsActiveTriageMailOperation(view.ReplyOperation);
                view.ReplyOperationKey = NewRetainedTriageOperationKey(retainedMail.Summary.Id);

                var mailboxes = await ports.ApprovedMailboxes.ListAsync(cancellationToken);
                view.ReplyMailbox = mailboxes.SingleOrDefault(item =>
                    item.Id == retainedMail.Summary.MailboxId
                    && item.State == ApprovedMailboxState.Approved
                    && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                    && item.Generation > 0);

                var replyRecipients = TriageReplyRecipients(retainedMail);
                view.ReplyTo = string.Join("; ", replyRecipients.Select(r => r.Address));
                view.ReplySubject = TriageReplySubject(retainedMail.Summary.Subject);
                if (view.IsOutcomeReply)
                {
                    view.ReplyBody = TriageOutcomeReply.Render(
                        triage.Record.NormalizedVehicleRegistration,
                        view.CurrentFinding);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Link case and Unlink case: the Triage edit scope for this save and the
    /// instruction Case's edit lease, both claimed here and both checked by
    /// Core; neither stands in for the other.
    /// </summary>
    private async Task<IActionResult> ExecuteTriageCaseAssociationAsync(
        bool linking,
        Guid triageCaseId,
        Guid caseId,
        long expectedTriageVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty
            || !Guid.TryParseExact(operationKey, "N", out var operationId))
        {
            TempData["TriageStatus"] =
                "A valid case and operation identity are required.";
            return RedirectToPage(new { id = triageCaseId });
        }

        try
        {
            var targetCase = await ports.GetCase.ExecuteAsync(
                new(caseId, actor),
                cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
            if (targetCase.ActiveEditLease is { } activeLease)
            {
                var unavailableReason = await DescribeTriageCaseHeldAsync(
                    activeLease,
                    actor,
                    ports,
                    cancellationToken);
                TempData["TriageStatus"] = unavailableReason;
                TempData["TriageUnavailableCase"] =
                    $"{caseId:D}|{unavailableReason}";
                return RedirectToPage(new { id = triageCaseId });
            }

            await TriageWriteAuthority.ExecuteAsync(
                ports.EditScopes,
                triageCaseId,
                expectedTriageVersion,
                actor,
                operationKey,
                logger,
                async token =>
                {
                    CaseEditLease? lease = null;
                    var leaseConsumed = false;
                    try
                    {
                        lease = await ports.CaseLeases.ClaimAsync(
                            new(
                                caseId,
                                targetCase.Workflow.Version,
                                actor,
                                $"triage-association-claim:{operationId:N}"),
                            cancellationToken);
                        var request = new TriageCaseLinkRequest(
                            triageCaseId,
                            caseId,
                            expectedTriageVersion,
                            lease.Version,
                            actor,
                            operationId.ToString("N"),
                            reason,
                            lease.Token)
                        {
                            EditLeaseToken = token
                        };
                        if (linking)
                        {
                            await ports.LinkCase.ExecuteAsync(request, cancellationToken);
                        }
                        else
                        {
                            await ports.UnlinkCase.ExecuteAsync(request, cancellationToken);
                        }

                        leaseConsumed = true;
                    }
                    finally
                    {
                        if (lease is not null && !leaseConsumed)
                        {
                            try
                            {
                                await ports.CaseLeases.ReleaseAsync(
                                    new(
                                        lease.CaseId,
                                        actor,
                                        $"triage-association-release:{operationId:N}",
                                        lease.Token),
                                    CancellationToken.None);
                            }
                            catch (Exception exception) when (IsExpectedTriageRefusal(exception))
                            {
                                TempData["TriageStatus"] =
                                    "The case association was not changed and its temporary edit authority could not be released immediately.";
                            }
                        }
                    }
                },
                cancellationToken);
            TempData["TriageStatus"] = linking ? Labels.CaseLinked : Labels.CaseUnlinked;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (EditScopeConflictException)
        {
            TempData["TriageStatus"] = await TriageWriteAuthority.DescribeHeldAsync(
                ports.EditScopes,
                ports.DescribeEditAuthorityHolder,
                triageCaseId,
                actor,
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            TempData["TriageStatus"] = Labels.Expired;
        }
        catch (EditScopeVersionConflictException)
        {
            TempData["TriageStatus"] = Labels.Changed;
        }
        catch (Exception exception) when (IsExpectedTriageRefusal(exception))
        {
            var unavailableReason = TriageRefusalMessage(exception);
            TempData["TriageStatus"] = unavailableReason;
            if (exception is CaseEditLeaseConflictException)
            {
                TempData["TriageUnavailableCase"] =
                    $"{caseId:D}|{unavailableReason}";
            }
        }

        return RedirectToPage(new { id = triageCaseId });
    }

    /// <summary>A staff actor whose subject is a staff account identity.</summary>
    private bool TryGetTriageActor(out ActionActor actor)
    {
        if (TryGetActor(out var resolved)
            && Guid.TryParse(resolved.SubjectId, out var staffId)
            && staffId != Guid.Empty)
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }

    private static (Guid PollOutcomeId, Guid SentEvidenceId) ParseTriageResponseCandidate(
        string? value)
    {
        var parts = value?.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 }
            || !Guid.TryParse(parts[0], out var pollOutcomeId)
            || pollOutcomeId == Guid.Empty
            || !Guid.TryParse(parts[1], out var sentEvidenceId)
            || sentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Select an authoritative approved-mailbox response candidate.",
                nameof(value));
        }

        return (pollOutcomeId, sentEvidenceId);
    }

    /// <summary>
    /// One wording and one clock for the case-edit disclosure, shared with the case workspace, so
    /// Triage never renders a subject identifier or a server-local time.
    /// </summary>
    private static async Task<string> DescribeTriageCaseHeldAsync(
        CaseEditLeaseSnapshot activeLease,
        ActionActor actor,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        var isSelf = CaseEditAuthority.IsHolder(
            activeLease.HolderKind,
            activeLease.Holder,
            actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await ports.DescribeEditAuthorityHolder.ExecuteAsync(
                activeLease.HolderKind,
                activeLease.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.CaseHeldBy(holder, isSelf);
    }

    /// <summary>
    /// Refusals reaching the operator are settled copy: a Core message names the case identifier
    /// and the internal edit-authority vocabulary, and neither belongs on the page.
    /// </summary>
    private static string TriageRefusalMessage(Exception exception) => exception switch
    {
        CaseEditLeaseConflictException =>
            "Case editing is unavailable because another member of staff is editing this case.",
        CaseEditLeaseExpiredException =>
            "Case editing is no longer available for this attempt. Reload the record and try again.",
        CaseVersionConflictException =>
            "The case changed while this was being prepared. Reload the record and try again.",
        _ => "The Triage action was not applied. Reload the record and try again."
    };

    private static string NewRetainedTriageOperationKey(Guid retainedMessageId) =>
        $"retained:{retainedMessageId:N}:{Guid.NewGuid():N}";

    private static bool IsRetainedTriageOperationKey(string? value, Guid retainedMessageId)
    {
        var prefix = $"retained:{retainedMessageId:N}:";
        return value is not null
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(value[prefix.Length..], "N", out _);
    }

    private static bool IsActiveTriageMailOperation(StaffMailOperation? operation) =>
        operation is not null
            && operation.State is not StaffMailState.Sent
                and not StaffMailState.Failed
                and not StaffMailState.Cancelled;

    private static StaffMailRecipient[] TriageReplyRecipients(RetainedMailDetail detail) =>
        ParseTriageRecipients(detail.ReplyToAddresses);

    private static StaffMailRecipient[] ParseTriageRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => new StaffMailRecipient(address.Trim(), DisplayName: null))
            .Where(item => IsTriageMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static StaffMailRecipient[] ParseTriageRecipients(IReadOnlyList<string>? values) =>
        (values ?? [])
            .SelectMany(val => (val ?? string.Empty).Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(address => new StaffMailRecipient(address.Trim(), DisplayName: null))
            .Where(item => IsTriageMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsTriageMailboxAddress(string value) =>
        System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    /// <summary>The reply keeps the original's subject with one "Re:".</summary>
    private static string TriageReplySubject(string? subject)
    {
        var value = subject?.Trim() ?? string.Empty;
        return value.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"Re: {value}".TrimEnd();
    }

    private static bool IsExpectedTriageRefusal(Exception exception) => exception is
        ArgumentException or InvalidOperationException or KeyNotFoundException;
}
