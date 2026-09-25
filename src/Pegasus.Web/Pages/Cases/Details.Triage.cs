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

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What a Triage Case's record renders on <c>/Cases/{id}</c>: the Triage
/// workspace (determinations, source, evidence images, exact response
/// evidence, chaser correspondence), the Case Files panel and the Triage notes.
/// </summary>
public sealed class TriageCaseView(TriageDetail triage)
{
    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    internal const string RecordName = "Triage record";

    public TriageDetail Triage { get; } = triage ?? throw new ArgumentNullException(nameof(triage));

    public TriageRecord Record => Triage.Record;

    /// <summary>The Triage Case's <c>t.</c> Case/PO.</summary>
    public string Reference => Triage.Record.Reference;

    public EditScopeLease? EditLease { get; set; }

    public bool IsEditing => EditLease is not null;

    /// <summary>An authorised editor may replace a live scope held in another window.</summary>
    public bool CanTakeOverEdit { get; set; }

    /// <summary>Each evidence image's recorded crop and tags, by its asset (pre-Case crop and tag, v26).</summary>
    public IReadOnlyDictionary<Guid, Pegasus.Core.ImageIntake.PreCaseImagePreparation> Preparations { get; set; } =
        new Dictionary<Guid, Pegasus.Core.ImageIntake.PreCaseImagePreparation>();

    /// <summary>The tag vocabulary the viewer's Tag select offers.</summary>
    public IReadOnlyList<Pegasus.Core.Documents.ImageTag> ImageTags { get; set; } = [];

    public IReadOnlyList<TriageFinding> ActiveFindings { get; set; } = [];

    public RetainedMailDetail? RetainedMail { get; set; }

    public StaffMailOperation? ChaserOperation { get; set; }

    public bool ChaserOperationBlocked { get; set; }

    public ApprovedMailbox? ChaserMailbox { get; set; }

    public string ChaserOperationKey { get; set; } = string.Empty;

    public string? ChaserTo { get; set; }

    public string? ChaserCc { get; set; }

    public string? ChaserSubject { get; set; }

    public string? ChaserBody { get; set; }

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

    /// <summary>Assign to me (Work Centre P8): an Engineer takes a Triage with no assignee.</summary>
    public bool CanAssignToMe { get; set; }

    public string? CaseAssociationUnavailableReason { get; set; }

    public Guid? CaseAssociationUnavailableCaseId { get; set; }

    public string OperationKey { get; set; } = StaffPageModel.NewOperationKey();

    public string? Message { get; set; }

    /// <summary>The enabled staff accounts this Triage may be assigned to.</summary>
    public IReadOnlyList<CaseEngineerChoice> EngineerChoices { get; set; } = [];

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

    public static string RoadworthinessLabel(RoadworthinessFinding finding) => finding switch
    {
        RoadworthinessFinding.Roadworthy => "Roadworthy",
        RoadworthinessFinding.Unroadworthy => "Unroadworthy",
        _ => throw new InvalidOperationException(
            $"Unknown roadworthiness finding value '{(int)finding}'.")
    };

    public static string AssessmentLabel(AssessmentFinding finding) => finding switch
    {
        AssessmentFinding.Repairable => "Repairable",
        AssessmentFinding.TotalLoss => "Total loss",
        _ => throw new InvalidOperationException(
            $"Unknown assessment finding value '{(int)finding}'.")
    };

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
        view.EngineerChoices = await ports.EngineerChoices.GetAsync(actor, cancellationToken);
        if (!view.IsEditing && StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework))
        {
            view.CanTakeOverEdit = await ports.EditScopes.GetActiveAsync(
                EditScopeKind.Triage, id, actor, cancellationToken) is not null;
        }

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

    public async Task<IActionResult> OnPostTriageAssignToMeAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await ports.AssignToMe.ExecuteAsync(
                new AssignTriageToMeRequest(id, expectedVersion, actor, operationKey),
                cancellationToken);
            TempData["TriageStatus"] = "The Triage was assigned to you.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsExpectedTriageRefusal(exception))
        {
            TempData["TriageStatus"] = "The Triage was not assigned because it changed or the action is not permitted.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTriageActionAsync(
        Guid id,
        string actionName,
        long expectedVersion,
        string operationKey,
        string reason,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        Guid? supersedesFindingId,
        string? responseCandidate,
        Guid? sentEvidenceId,
        Guid? caseId,
        Guid? assigneeId,
        string? note,
        string? editLeaseToken,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actionActor))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            if (!await LoadTriageCaseAsync(id, actionActor, ports, cancellationToken))
            {
                return NotFound();
            }

            TriageCase!.Message = "Select Edit Triage before changing this record.";
            return Page();
        }

        string? message;
        var nextOperationKey = operationKey;
        try
        {
            var mutation = new TriageMutationRequest(
                id,
                expectedVersion,
                actionActor,
                operationKey,
                reason)
            {
                EditLeaseToken = editLeaseToken
            };
            switch (actionName)
            {
                case "assign":
                    // The engineer is chosen explicitly. Defaulting to the
                    // signed-in staff member is what "Assign to me" did, and
                    // it made the roster invisible.
                    if (assigneeId is not { } chosenEngineer || chosenEngineer == Guid.Empty)
                    {
                        ModelState.AddModelError("assigneeId", "Choose the engineer to assign.");
                        return await GetTriageCaseAsync(id, actionActor, ports, cancellationToken);
                    }

                    await ports.Assign.ExecuteAsync(
                        new(
                            id,
                            expectedVersion,
                            chosenEngineer,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                case "note":
                    await ports.AddNote.ExecuteAsync(
                        new(id, expectedVersion, actionActor, operationKey, note ?? string.Empty)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                case "unassign":
                    await ports.Unassign.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "await_information":
                    await ports.AwaitInformation.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "record_finding":
                    await ports.RecordFinding.ExecuteAsync(
                        new(
                            id,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason,
                            roadworthiness,
                            assessment,
                            null)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                case "supersede_finding":
                    await ports.SupersedeFinding.ExecuteAsync(
                        new(
                            id,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason,
                            roadworthiness,
                            assessment,
                            supersedesFindingId)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                case "link_response":
                {
                    var candidate = ParseTriageResponseCandidate(responseCandidate);
                    await ports.LinkResponseEvidence.ExecuteAsync(
                        new(
                            id,
                            candidate.PollOutcomeId,
                            candidate.SentEvidenceId,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                }
                case "unlink_response":
                    await ports.UnlinkResponseEvidence.ExecuteAsync(
                        new(
                            id,
                            sentEvidenceId ?? Guid.Empty,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken
                        },
                        cancellationToken);
                    break;
                case "complete":
                    await ports.Complete.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "cancel":
                    await ports.Cancel.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "reopen":
                    await ports.Reopen.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "link_case":
                    return await ExecuteTriageCaseAssociationAsync(
                        linking: true,
                        id,
                        caseId ?? Guid.Empty,
                        expectedVersion,
                        actionActor,
                        operationKey,
                        reason,
                        editLeaseToken,
                        ports,
                        cancellationToken);
                case "unlink_case":
                    return await ExecuteTriageCaseAssociationAsync(
                        linking: false,
                        id,
                        caseId ?? Guid.Empty,
                        expectedVersion,
                        actionActor,
                        operationKey,
                        reason,
                        editLeaseToken,
                        ports,
                        cancellationToken);
                default:
                    throw new ArgumentException("The requested Triage action is not supported.");
            }

            message = "Triage workflow updated.";
            nextOperationKey = NewOperationKey();
        }
        catch (EditScopeConflictException)
        {
            message = await DescribeTriageHeldAsync(id, actionActor, ports, cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            message = "Editing expired before this change was saved. Reload and try again.";
        }
        catch (EditScopeVersionConflictException)
        {
            await ReleaseRefusedTriageEditAsync(id, actionActor, editLeaseToken, ports, cancellationToken);
            message = "This Triage record changed while you were working. Reload and try again.";
        }
        catch (Exception exception) when (IsExpectedTriageRefusal(exception))
        {
            await ReleaseRefusedTriageEditAsync(id, actionActor, editLeaseToken, ports, cancellationToken);
            message = exception.Message;
        }

        if (!await LoadTriageCaseAsync(id, actionActor, ports, cancellationToken))
        {
            return NotFound();
        }

        TriageCase!.Message = message;
        TriageCase.OperationKey = nextOperationKey;
        return Page();
    }

    public async Task<IActionResult> OnPostTriageEditAsync(
        Guid id,
        long expectedVersion,
        bool takeOver,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actor))
        {
            return Forbid();
        }

        EditScopeLease? editLease = null;
        var canTakeOver = false;
        try
        {
            editLease = await ports.EditScopes.ClaimAsync(
                new(EditScopeKind.Triage, id, expectedVersion, actor, $"triage-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
        }
        catch (EditScopeHeldElsewhereException)
        {
            canTakeOver = true;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(TriageCaseView.RecordName));
        }
        catch (EditScopeConflictException)
        {
            canTakeOver = true;
            ModelState.AddModelError(string.Empty,
                await DescribeTriageHeldAsync(id, actor, ports, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty,
                "This Triage record changed while you were working. Reload and try again.");
        }

        if (!await LoadTriageCaseAsync(id, actor, ports, cancellationToken))
        {
            return NotFound();
        }

        TriageCase!.EditLease = editLease;
        TriageCase.CanTakeOverEdit = canTakeOver;
        return Page();
    }

    public async Task<IActionResult> OnPostTriageCancelEditAsync(
        Guid id,
        string editLeaseToken,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await ports.EditScopes.ReleaseAsync(
                new(EditScopeKind.Triage, id, actor, $"triage-edit-release:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // The already-expired scope protects no mutation and is treated as cancelled.
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTriageHeartbeatEditAsync(
        Guid id,
        string editLeaseToken,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await ports.EditScopes.HeartbeatAsync(
                new(EditScopeKind.Triage, id, actor, editLeaseToken), cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this Triage record has ended. Reload it before making further changes.");
        }
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostTriageReleaseScopeBeaconAsync(
        Guid id,
        string? editLeaseToken,
        [FromServices] TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ports);
        if (!TryGetTriageActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await ports.EditScopes.ReleaseAsync(
                new(EditScopeKind.Triage, id, actor, $"triage-edit-beacon:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }

    public async Task<IActionResult> OnPostTriageSendChaserAsync(
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

        // A chaser replies to the mailbox request a Triage Case was opened from.
        if (triage.Record.Origin is not { } origin
            || origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            ModelState.AddModelError(string.Empty, "A chaser reply can only be sent for mailbox intake.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken);
        }

        if (triage.Record.Version != expectedVersion)
        {
            ModelState.AddModelError(
                string.Empty,
                "The triage record changed while this was being prepared. Reload the record and try again.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken);
        }

        var detail = await ports.RetainedMail.ExecuteByOriginReceiptAsync(
            actionActor,
            origin.ReceiptId,
            cancellationToken);
        if (detail is null)
        {
            ModelState.AddModelError(string.Empty, "Originating retained message was not found.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken);
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
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken);
        }

        if (!IsRetainedTriageOperationKey(operationKey, detail.Summary.Id))
        {
            ModelState.AddModelError(
                nameof(operationKey),
                "The send operation key is invalid or has expired.");
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken);
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
        IReadOnlyList<string> chosenAttachments = selectedAttachments ?? [];
        try
        {
            attachments = await ports.AttachmentResolver.ResolveIntakeAsync(
                actionActor, origin.ReceiptId, chosenAttachments,
                cancellationToken);
        }
        catch (StaffMailAttachmentSelectionException exception)
        {
            ModelState.AddModelError(nameof(selectedAttachments), exception.Message);
        }

        if (!ModelState.IsValid)
        {
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken, chosenAttachments);
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
            Purpose: StaffMailPurpose.TriageChaser,
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
            if (operation.State == StaffMailState.Sent)
            {
                TempData["TriageStatus"] = "Triage chaser sent.";
            }
            else
            {
                TempData["TriageStatus"] = $"Triage chaser status: {OperatorLabels.StaffMail.State(operation.State)}.";
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken, chosenAttachments);
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
            return await ReloadTriageCaseAsync(id, actionActor, ports, cancellationToken, chosenAttachments);
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTriageReconcileChaserAsync(
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

        if (operation.Purpose != StaffMailPurpose.TriageChaser
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

    private async Task<IActionResult> ReloadTriageCaseAsync(
        Guid id,
        ActionActor actor,
        TriageCasePorts ports,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? selectedAttachments = null)
    {
        if (!await LoadTriageCaseAsync(id, actor, ports, cancellationToken))
        {
            return NotFound();
        }

        if (selectedAttachments is not null)
        {
            TriageCase!.SelectedAttachments = selectedAttachments;
        }

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
        var roster = await ports.EngineerChoices.GetAsync(actor, cancellationToken);
        view.AssigneeName = triage.Record.AssigneeId is { } assigneeId
            ? roster.FirstOrDefault(choice => choice.StaffId == assigneeId)?.DisplayName ?? "Assigned"
            : null;
        view.CanAssignToMe = TriageLifecycleRules.CanAssignToSelf(triage.Record)
            && NeedsAttentionPolicy.CanTake(NeedsAttentionKind.Triage, actor);

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
            else if (linkedCase.ActiveEditLease is { } activeLease)
            {
                view.CaseAssociationUnavailableReason = await DescribeTriageCaseHeldAsync(
                    activeLease,
                    actor,
                    ports,
                    cancellationToken);
                view.CaseAssociationUnavailableCaseId = linkedCaseId;
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
                view.ChaserOperation = await ports.StaffMailSend.GetLatestForOriginalAsync(
                    actor,
                    retainedMail.Summary.Id,
                    cancellationToken);
                view.ChaserOperationBlocked = IsActiveTriageMailOperation(view.ChaserOperation);
                view.ChaserOperationKey = NewRetainedTriageOperationKey(retainedMail.Summary.Id);

                var mailboxes = await ports.ApprovedMailboxes.ListAsync(cancellationToken);
                view.ChaserMailbox = mailboxes.SingleOrDefault(item =>
                    item.Id == retainedMail.Summary.MailboxId
                    && item.State == ApprovedMailboxState.Approved
                    && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                    && item.Generation > 0);

                var replyRecipients = TriageReplyRecipients(retainedMail);
                view.ChaserTo = string.Join("; ", replyRecipients.Select(r => r.Address));
                view.ChaserSubject = TriageReplySubject(retainedMail.Summary.Subject);
            }
        }

        return true;
    }

    private async Task<IActionResult> ExecuteTriageCaseAssociationAsync(
        bool linking,
        Guid triageCaseId,
        Guid caseId,
        long expectedTriageVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string editLeaseToken,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty
            || !Guid.TryParseExact(operationKey, "N", out var operationId))
        {
            await ReleaseRefusedTriageEditAsync(triageCaseId, actor, editLeaseToken, ports, cancellationToken);
            TempData["TriageStatus"] =
                "A valid case and operation identity are required.";
            return RedirectToPage(new { id = triageCaseId });
        }

        CaseEditLease? lease = null;
        var leaseConsumed = false;
        try
        {
            var targetCase = await ports.GetCase.ExecuteAsync(
                new(caseId, actor),
                cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
            if (targetCase.ActiveEditLease is { } activeLease)
            {
                await ReleaseRefusedTriageEditAsync(triageCaseId, actor, editLeaseToken, ports, cancellationToken);
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
                EditLeaseToken = editLeaseToken
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
            TempData["TriageStatus"] = linking
                ? "The Triage record was linked to the case."
                : "The Triage case association was removed.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsExpectedTriageRefusal(exception))
        {
            await ReleaseRefusedTriageEditAsync(triageCaseId, actor, editLeaseToken, ports, cancellationToken);
            var unavailableReason = TriageRefusalMessage(exception);
            TempData["TriageStatus"] = unavailableReason;
            if (exception is CaseEditLeaseConflictException)
            {
                TempData["TriageUnavailableCase"] =
                    $"{caseId:D}|{unavailableReason}";
            }
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

    private static async Task<string> DescribeTriageHeldAsync(
        Guid triageCaseId,
        ActionActor actor,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        var active = await ports.EditScopes.GetActiveAsync(
            EditScopeKind.Triage, triageCaseId, actor, cancellationToken);
        if (active is null)
        {
            return "Another member of staff is editing this Triage record. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await ports.DescribeEditAuthorityHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(TriageCaseView.RecordName, holder, isSelf);
    }

    private static async Task ReleaseRefusedTriageEditAsync(
        Guid triageCaseId,
        ActionActor actor,
        string? editLeaseToken,
        TriageCasePorts ports,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return;
        }

        try
        {
            await ports.EditScopes.ReleaseAsync(
                new(EditScopeKind.Triage, triageCaseId, actor,
                    $"triage-refused-edit-release:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // A refused action has no record mutation left to protect.
        }
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

    /// <summary>The chaser is a reply, so its subject is the original's with one "Re:".</summary>
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
