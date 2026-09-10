using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Triage;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IGetTriage getTriage,
    IGetCase getCase,
    ILeaseCaseForEdit caseLeases,
    IAssignTriage assign,
    IUnassignTriage unassign,
    IAwaitTriageInformation awaitInformation,
    IRecordTriageFinding recordFinding,
    ISupersedeTriageFinding supersedeFinding,
    ILinkTriageResponseEvidence linkResponseEvidence,
    IUnlinkTriageResponseEvidence unlinkResponseEvidence,
    ICompleteTriage complete,
    ICancelTriage cancel,
    IReopenTriage reopen,
    ILinkTriageCase linkCase,
    IUnlinkTriageCase unlinkCase,
    IEditScopeLeases editScopes,
    IGetIntake getIntake,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    ICaseEngineerChoices engineerChoices,
    IAddTriageNote addNote,
    ISetTriagePrincipal setPrincipal,
    ITriageQueries triageQueries,
    GetRetainedMail? getRetainedMail = null,
    IStaffMailSend? staffMailSend = null,
    IApprovedMailboxStore? approvedMailboxes = null,
    IStaffMailAttachmentResolver? attachmentResolver = null) : StaffPageModel
{
    private readonly IGetTriage _getTriage =
        getTriage ?? throw new ArgumentNullException(nameof(getTriage));

    public EditScopeLease? EditLease { get; private set; }

    public bool IsEditing => EditLease is not null;

    /// <summary>
    /// Set when this operator is already editing this Triage record in another
    /// window, so the page offers the take-over that ends the other window's
    /// claim instead of an Edit that would be refused again.
    /// </summary>
    public bool CanTakeOverEdit { get; private set; }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "Triage record";
    private readonly IGetCase _getCase =
        getCase ?? throw new ArgumentNullException(nameof(getCase));
    private readonly IGetIntake _getIntake =
        getIntake ?? throw new ArgumentNullException(nameof(getIntake));
    private readonly ILeaseCaseForEdit _caseLeases =
        caseLeases ?? throw new ArgumentNullException(nameof(caseLeases));
    private readonly IDescribeCaseEditAuthorityHolder _describeEditAuthorityHolder =
        describeEditAuthorityHolder
            ?? throw new ArgumentNullException(nameof(describeEditAuthorityHolder));
    private readonly GetRetainedMail? _getRetainedMail = getRetainedMail;
    private readonly IStaffMailSend? _staffMailSend = staffMailSend;
    private readonly IApprovedMailboxStore? _approvedMailboxes = approvedMailboxes;
    private readonly IStaffMailAttachmentResolver? _attachmentResolver = attachmentResolver;


    public TriageDetail Triage { get; private set; } = null!;

    public IReadOnlyList<TriageFinding> ActiveFindings { get; private set; } = [];

    public RetainedMailDetail? RetainedMail { get; private set; }

    public StaffMailOperation? ChaserOperation { get; private set; }

    public bool ChaserOperationBlocked { get; private set; }

    public ApprovedMailbox? ChaserMailbox { get; private set; }

    public string ChaserOperationKey { get; private set; } = string.Empty;

    public string? ChaserTo { get; private set; }

    public string? ChaserCc { get; private set; }

    public string? ChaserSubject { get; private set; }

    public string? ChaserBody { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public IReadOnlyList<string> SelectedAttachments { get; private set; } = [];

    /// <summary>
    /// The photographs the provider attached to the Triage request. A Triage
    /// has no evidence of its own in the domain — these are the origin
    /// receipt's own retained assets, read through the one selection owner and
    /// served by the one authorised asset route. Nothing is copied or retained
    /// a second time (INTK-034).
    /// </summary>
    public IReadOnlyList<IntakeAssetRecord> EvidenceImages { get; private set; } = [];

    /// <summary>
    /// What the link to the origin receipt opens, named for the material
    /// itself: an e-mail for mailbox material, a file otherwise. Classified by
    /// the same Core rule the Unidentified page uses
    /// (<see cref="UnidentifiedMediaKindPolicy"/>) so one retained source is
    /// not called two different things on two screens.
    /// </summary>
    public string ViewSourceLabel { get; private set; } = "View file";
    public string? CaseAssociationUnavailableReason { get; private set; }
    public Guid? CaseAssociationUnavailableCaseId { get; private set; }




    public string OperationKey { get; private set; } = NewOperationKey();

    public string? Message { get; private set; }

    /// <summary>
    /// The engineers this Triage may be assigned to — the enabled accounts
    /// holding the Engineer role, and nobody else.
    /// </summary>
    public IReadOnlyList<CaseEngineerChoice> EngineerChoices { get; private set; } = [];

    /// <summary>
    /// The active principals a staff member may record against this Triage —
    /// the same option list <c>ImageIntake.Details</c> offers.
    /// </summary>
    public IReadOnlyList<Principal> PrincipalOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }

        EngineerChoices = await engineerChoices.GetAsync(actor, cancellationToken);

        Message = TempData["TriageStatus"] as string;
        if (TempData["TriageUnavailableCase"] is string unavailableCase)
        {
            var separator = unavailableCase.IndexOf('|');
            if (separator > 0
                && Guid.TryParse(unavailableCase.AsSpan(0, separator), out var parsedCaseId)
                && parsedCaseId != Guid.Empty)
            {
                CaseAssociationUnavailableCaseId = parsedCaseId;
                CaseAssociationUnavailableReason = unavailableCase[(separator + 1)..];
            }
        }

        return Page();
    }


    public async Task<IActionResult> OnPostActionAsync(
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
        Guid? principalId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var staffId, out var actionActor))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            Message = "Select Edit Triage before changing this record.";
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        OperationKey = operationKey;
        try
        {
            var mutation = new TriageMutationRequest(
                id,
                expectedVersion,
                actionActor,
                operationKey,
                reason)
            {
                EditLeaseToken = editLeaseToken ?? string.Empty
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
                        return await OnGetAsync(id, cancellationToken);
                    }

                    await assign.ExecuteAsync(
                        new(
                            id,
                            expectedVersion,
                            chosenEngineer,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "note":
                    await addNote.ExecuteAsync(
                        new(id, expectedVersion, actionActor, operationKey, note ?? string.Empty)
                        {
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "set_principal":
                    // No reason: recording who the work is for is casework,
                    // not a lifecycle transition (mirrors Image Intake).
                    await setPrincipal.ExecuteAsync(
                        new(id, principalId, actionActor, expectedVersion)
                        {
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "unassign":
                    await unassign.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "await_information":
                    await awaitInformation.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "record_finding":
                    await recordFinding.ExecuteAsync(
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
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "supersede_finding":
                    await supersedeFinding.ExecuteAsync(
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
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "link_response":
                {
                    var candidate = ParseResponseCandidate(responseCandidate);
                    await linkResponseEvidence.ExecuteAsync(
                        new(
                            id,
                            candidate.PollOutcomeId,
                            candidate.SentEvidenceId,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                }
                case "unlink_response":
                    await unlinkResponseEvidence.ExecuteAsync(
                        new(
                            id,
                            sentEvidenceId ?? Guid.Empty,
                            expectedVersion,
                            actionActor,
                            operationKey,
                            reason)
                        {
                            EditLeaseToken = editLeaseToken ?? string.Empty
                        },
                        cancellationToken);
                    break;
                case "complete":
                    await complete.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "cancel":
                    await cancel.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "reopen":
                    await reopen.ExecuteAsync(mutation, cancellationToken);
                    break;
                case "link_case":
                    return await ExecuteCaseAssociationAsync(
                        linking: true,
                        id,
                        caseId ?? Guid.Empty,
                        expectedVersion,
                        actionActor,
                        operationKey,
                        reason,
                        editLeaseToken ?? string.Empty,
                        cancellationToken);
                case "unlink_case":
                    return await ExecuteCaseAssociationAsync(
                        linking: false,
                        id,
                        caseId ?? Guid.Empty,
                        expectedVersion,
                        actionActor,
                        operationKey,
                        reason,
                        editLeaseToken ?? string.Empty,
                        cancellationToken);
                default:
                    throw new ArgumentException("The requested Triage action is not supported.");
            }

            Message = "Triage workflow updated.";
            OperationKey = NewOperationKey();
        }
        catch (EditScopeConflictException)
        {
            Message = await DescribeTriageHeldAsync(id, actionActor, cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            Message = "Editing expired before this change was saved. Reload and try again.";
        }
        catch (EditScopeVersionConflictException)
        {
            await ReleaseRefusedEditAsync(id, actionActor, editLeaseToken, cancellationToken);
            Message = "This Triage record changed while you were working. Reload and try again.";
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            await ReleaseRefusedEditAsync(id, actionActor, editLeaseToken, cancellationToken);
            Message = exception.Message;
        }

        return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
    }

    public static string StateLabel(TriageState state) => Presentation.OperatorLabels.TriageState(state);

    public async Task<IActionResult> OnPostEditAsync(
        Guid id,
        long expectedVersion,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            EditLease = await editScopes.ClaimAsync(
                new(EditScopeKind.Triage, id, expectedVersion, actor, $"triage-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
        }
        catch (EditScopeHeldElsewhereException)
        {
            CanTakeOverEdit = true;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty,
                await DescribeTriageHeldAsync(id, actor, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty,
                "This Triage record changed while you were working. Reload and try again.");
        }

        return await LoadAsync(id, actor, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostCancelEditAsync(
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.Triage, id, actor, $"triage-edit-release:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // The already-expired scope protects no mutation and is treated as cancelled.
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostHeartbeatEditAsync(
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await editScopes.HeartbeatAsync(
                new(EditScopeKind.Triage, id, actor, editLeaseToken), cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this Triage record has ended. Reload it before making further changes.");
        }
    }

    public static string SourceChannelLabel(IntakeSourceChannel channel) =>
        Presentation.OperatorLabels.SourceChannel(channel);

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
        "triage_principal_set" => "Principal recorded",
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

    private async Task<bool> LoadAsync(
        Guid id,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var triage = await _getTriage.ExecuteAsync(new(id, actor), cancellationToken);
        if (triage is null)
        {
            return false;
        }

        Triage = triage;
        PrincipalOptions = await triageQueries.ListActivePrincipalsAsync(cancellationToken);
        // The request's photographs are the whole subject of the assessment,
        // and until now they were viewable nowhere: the "View e-mail" link
        // lands on a page that lists attachments by name without rendering
        // one. No rights guard here: GetTriage above already required the
        // same PerformCasework right from the same actor, so this cannot be
        // reached without it.
        var receipt = await _getIntake.ExecuteAsync(
            new(triage.Record.Origin.ReceiptId, actor),
            cancellationToken);
        EvidenceImages = receipt is null
            ? []
            : InstructionEvidenceImages.Select(receipt.AssetRecords);
        var sourceIsEmail = receipt is not null && !string.IsNullOrWhiteSpace(receipt.MediaType)
            ? UnidentifiedMediaKindPolicy.Classify(
                receipt.SourceIdentity.Channel,
                receipt.MediaType) == UnidentifiedMediaKind.Email
            : triage.Record.Origin.SourceIdentity.Channel == IntakeSourceChannel.Mailbox;
        ViewSourceLabel = sourceIsEmail ? "View email" : "View file";

        ActiveFindings = triage.Findings
            .Where(candidate => !triage.Findings.Any(
                finding => finding.SupersedesFindingId == candidate.Id))
            .ToArray();
        CaseAssociationUnavailableReason = null;
        CaseAssociationUnavailableCaseId = null;
        if (triage.Record.LinkedCaseId is { } linkedCaseId)
        {
            var linkedCase = await _getCase.ExecuteAsync(
                new(linkedCaseId, actor),
                cancellationToken);
            if (linkedCase is null)
            {
                CaseAssociationUnavailableReason =
                    "The linked case is unavailable. Case association is read-only.";
                CaseAssociationUnavailableCaseId = linkedCaseId;
            }
            else if (linkedCase.ActiveEditLease is { } activeLease)
            {
                CaseAssociationUnavailableReason = await DescribeCaseHeldAsync(
                    activeLease,
                    actor,
                    cancellationToken);
                CaseAssociationUnavailableCaseId = linkedCaseId;
            }
        }

        if (triage.Record.Origin.SourceIdentity.Channel == IntakeSourceChannel.Mailbox
            && _getRetainedMail is not null
            && _staffMailSend is not null
            && _approvedMailboxes is not null)
        {
            RetainedMail = await _getRetainedMail.ExecuteByOriginReceiptAsync(
                actor,
                triage.Record.Origin.ReceiptId,
                cancellationToken);
            if (RetainedMail is not null)
            {
                if (_attachmentResolver is not null)
                {
                    AvailableAttachments = await _attachmentResolver.ListIntakeAsync(
                        actor, triage.Record.Origin.ReceiptId, cancellationToken);
                }
                ChaserOperation = await _staffMailSend.GetLatestForOriginalAsync(
                    actor,
                    RetainedMail.Summary.Id,
                    cancellationToken);
                ChaserOperationBlocked = IsActiveOperation(ChaserOperation);
                ChaserOperationKey = NewRetainedOperationKey(RetainedMail.Summary.Id);

                var mailboxes = await _approvedMailboxes.ListAsync(cancellationToken);
                ChaserMailbox = mailboxes.SingleOrDefault(item =>
                    item.Id == RetainedMail.Summary.MailboxId
                    && item.State == ApprovedMailboxState.Approved
                    && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                    && item.Generation > 0);

                var replyRecipients = ReplyRecipients(RetainedMail);
                ChaserTo = string.Join("; ", replyRecipients.Select(r => r.Address));
                ChaserSubject = SubjectFor(StaffMailComposeMode.Reply, RetainedMail.Summary.Subject);
            }
        }

        return true;
    }

    private async Task<IActionResult> ExecuteCaseAssociationAsync(
        bool linking,
        Guid triageId,
        Guid caseId,
        long expectedTriageVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty
            || !Guid.TryParseExact(operationKey, "N", out var operationId))
        {
            await ReleaseRefusedEditAsync(triageId, actor, editLeaseToken, cancellationToken);
            TempData["TriageStatus"] =
                "A valid case and operation identity are required.";
            return RedirectToPage(new { id = triageId });
        }

        CaseEditLease? lease = null;
        var leaseConsumed = false;
        try
        {
            var targetCase = await _getCase.ExecuteAsync(
                new(caseId, actor),
                cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
            if (targetCase.ActiveEditLease is { } activeLease)
            {
                await ReleaseRefusedEditAsync(triageId, actor, editLeaseToken, cancellationToken);
                var unavailableReason = await DescribeCaseHeldAsync(
                    activeLease,
                    actor,
                    cancellationToken);
                TempData["TriageStatus"] = unavailableReason;
                TempData["TriageUnavailableCase"] =
                    $"{caseId:D}|{unavailableReason}";
                return RedirectToPage(new { id = triageId });
            }

            lease = await _caseLeases.ClaimAsync(
                new(
                    caseId,
                    targetCase.Workflow.Version,
                    actor,
                    $"triage-association-claim:{operationId:N}"),
                cancellationToken);
            var request = new TriageCaseLinkRequest(
                triageId,
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
                await linkCase.ExecuteAsync(request, cancellationToken);
            }
            else
            {
                await unlinkCase.ExecuteAsync(request, cancellationToken);
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
        catch (Exception exception) when (IsExpected(exception))
        {
            await ReleaseRefusedEditAsync(triageId, actor, editLeaseToken, cancellationToken);
            var unavailableReason = RefusalMessage(exception);
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
                    await _caseLeases.ReleaseAsync(
                        new(
                            lease.CaseId,
                            actor,
                            $"triage-association-release:{operationId:N}",
                            lease.Token),
                        CancellationToken.None);
                }
                catch (Exception exception) when (IsExpected(exception))
                {
                    TempData["TriageStatus"] =
                        "The case association was not changed and its temporary edit authority could not be released immediately.";
                }
            }
        }

        return RedirectToPage(new { id = triageId });
    }

    private bool TryGetActor(out Guid staffId, out ActionActor actor)
    {
        if (TryGetActor(out var resolved)
            && Guid.TryParse(resolved.SubjectId, out staffId)
            && staffId != Guid.Empty)
        {
            actor = resolved;
            return true;
        }

        staffId = Guid.Empty;
        actor = null!;
        return false;
    }

    private static (Guid PollOutcomeId, Guid SentEvidenceId) ParseResponseCandidate(
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
    private async Task<string> DescribeCaseHeldAsync(
        CaseEditLeaseSnapshot activeLease,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var isSelf = CaseEditAuthority.IsHolder(
            activeLease.HolderKind,
            activeLease.Holder,
            actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await _describeEditAuthorityHolder.ExecuteAsync(
                activeLease.HolderKind,
                activeLease.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.CaseHeldBy(holder, isSelf);
    }

    private async Task<string> DescribeTriageHeldAsync(
        Guid triageId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var active = await editScopes.GetActiveAsync(
            EditScopeKind.Triage, triageId, actor, cancellationToken);
        if (active is null)
        {
            return "Another member of staff is editing this Triage record. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await _describeEditAuthorityHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(RecordName, holder, isSelf);
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(
        Guid id,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await editScopes.ReleaseAsync(
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

    private async Task ReleaseRefusedEditAsync(
        Guid triageId,
        ActionActor actor,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return;
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.Triage, triageId, actor,
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
    private static string RefusalMessage(Exception exception) => exception switch
    {
        CaseEditLeaseConflictException =>
            "Case editing is unavailable because another member of staff is editing this case.",
        CaseEditLeaseExpiredException =>
            "Case editing is no longer available for this attempt. Reload the record and try again.",
        CaseVersionConflictException =>
            "The case changed while this was being prepared. Reload the record and try again.",
        _ => "The Triage action was not applied. Reload the record and try again."
    };

    public async Task<IActionResult> OnPostSendChaserAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? to,
        string? cc,
        string? subject,
        string? body,
        List<string>? selectedAttachments,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var actionActor))
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

        if (_getRetainedMail is null || _staffMailSend is null || _approvedMailboxes is null
            || _attachmentResolver is null)
        {
            return NotFound();
        }

        var triage = await _getTriage.ExecuteAsync(new(id, actionActor), cancellationToken);
        if (triage is null)
        {
            return NotFound();
        }

        if (triage.Record.Origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            ModelState.AddModelError(string.Empty, "A chaser reply can only be sent for mailbox intake.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        if (triage.Record.Version != expectedVersion)
        {
            ModelState.AddModelError(
                string.Empty,
                "The triage record changed while this was being prepared. Reload the record and try again.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        var detail = await _getRetainedMail.ExecuteByOriginReceiptAsync(
            actionActor,
            triage.Record.Origin.ReceiptId,
            cancellationToken);
        if (detail is null)
        {
            ModelState.AddModelError(string.Empty, "Originating retained message was not found.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        var mailboxes = await _approvedMailboxes.ListAsync(cancellationToken);
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
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        if (!IsRetainedOperationKey(operationKey, detail.Summary.Id))
        {
            ModelState.AddModelError(
                nameof(operationKey),
                "The send operation key is invalid or has expired.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        var toRecipients = ParseRecipients(to);
        if (toRecipients.Length == 0)
        {
            toRecipients = ReplyRecipients(detail);
        }
        var ccRecipients = ParseRecipients(cc);

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
        SelectedAttachments = selectedAttachments ?? [];
        try
        {
            attachments = await _attachmentResolver.ResolveIntakeAsync(
                actionActor, triage.Record.Origin.ReceiptId, SelectedAttachments,
                cancellationToken);
        }
        catch (StaffMailAttachmentSelectionException exception)
        {
            ModelState.AddModelError(nameof(selectedAttachments), exception.Message);
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
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
            ContextId: triage.Record.Id,
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
            var operation = await _staffMailSend.SendAsync(command, cancellationToken);
            ChaserOperation = operation;
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
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }
        catch (InvalidOperationException)
        {
            var currentOperation = await _staffMailSend.GetLatestForOriginalAsync(
                actionActor,
                detail.Summary.Id,
                cancellationToken);
            if (!IsActiveOperation(currentOperation))
            {
                throw;
            }
            ModelState.AddModelError(
                string.Empty,
                "The existing correspondence operation must finish or be resolved before another action.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReconcileChaserAsync(
        Guid id,
        Guid operationId,
        long expectedOperationVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var actionActor))
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

        if (_staffMailSend is null || _getRetainedMail is null)
        {
            return NotFound();
        }

        var triage = await _getTriage.ExecuteAsync(new(id, actionActor), cancellationToken);
        if (triage is null)
        {
            return NotFound();
        }

        if (triage.Record.Origin.SourceIdentity.Channel != IntakeSourceChannel.Mailbox)
        {
            return NotFound();
        }

        var detail = await _getRetainedMail.ExecuteByOriginReceiptAsync(
            actionActor,
            triage.Record.Origin.ReceiptId,
            cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var operation = await _staffMailSend.GetAsync(
            actionActor,
            operationId,
            cancellationToken);
        if (operation is null)
        {
            return NotFound();
        }

        if (operation.Purpose != StaffMailPurpose.TriageChaser
            || operation.ContextId != triage.Record.Id
            || operation.OriginalRetainedMessageId is null
            || operation.OriginalRetainedMessageId != detail.Summary.Id)
        {
            return NotFound();
        }

        if (operation.ExpectedContextVersion != triage.Record.Version)
        {
            Message = "The triage workflow was updated concurrently. Refresh and review the latest state.";
            ModelState.AddModelError(
                string.Empty,
                "The triage workflow was updated concurrently. Refresh and review the latest state.");
            return await LoadAsync(id, actionActor, cancellationToken) ? Page() : NotFound();
        }

        try
        {
            await _staffMailSend.ReconcileAsync(
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

    private static string NewRetainedOperationKey(Guid retainedMessageId) =>
        $"retained:{retainedMessageId:N}:{Guid.NewGuid():N}";

    private static bool IsRetainedOperationKey(string? value, Guid retainedMessageId)
    {
        var prefix = $"retained:{retainedMessageId:N}:";
        return value is not null
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(value[prefix.Length..], "N", out _);
    }

    private static bool IsActiveOperation(StaffMailOperation? operation) =>
        operation is not null
            && operation.State is not StaffMailState.Sent
                and not StaffMailState.Failed
                and not StaffMailState.Cancelled;

    private static StaffMailRecipient[] ReplyRecipients(RetainedMailDetail detail) =>
        ParseRecipients(detail.ReplyToAddresses);

    private static StaffMailRecipient[] ParseRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => new StaffMailRecipient(address.Trim(), DisplayName: null))
            .Where(item => IsMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static StaffMailRecipient[] ParseRecipients(IReadOnlyList<string>? values) =>
        (values ?? [])
            .SelectMany(val => (val ?? string.Empty).Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(address => new StaffMailRecipient(address.Trim(), DisplayName: null))
            .Where(item => IsMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsMailboxAddress(string value) =>
        System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    private static string SubjectFor(StaffMailComposeMode mode, string? subject)
    {
        var value = subject?.Trim() ?? string.Empty;
        return mode switch
        {
            StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll
                when value.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) => value,
            StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll => $"Re: {value}".TrimEnd(),
            StaffMailComposeMode.Forward when value.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) => value,
            StaffMailComposeMode.Forward => $"Fwd: {value}".TrimEnd(),
            _ => value
        };
    }

    private static bool IsExpected(Exception exception) => exception is
        ArgumentException or InvalidOperationException or KeyNotFoundException;
}
