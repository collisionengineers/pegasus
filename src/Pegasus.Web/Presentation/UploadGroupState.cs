using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>One member of a stored upload: its queue status, its receipt (processed, else staged) and its confirmation outcome.</summary>
public sealed record UploadGroupMemberState(
    IntakeSubmissionGroupMember Member,
    QueuedIntakeStatus? Status,
    IntakeReceipt? Receipt,
    UploadOutcomeView? Outcome);

/// <summary>
/// A stored upload of several files as its one submission decision reads it (v30
/// Upload E): each member's status, receipt and outcome, whether every member may be
/// attached to one Case now, and whether the upload may be discarded. The upload
/// group page and the Automation upload-group tools both read it here and apply its
/// two roster checks, so both decide on the same facts.
/// </summary>
public sealed record UploadGroupState(
    IntakeSubmissionGroup Group,
    IReadOnlyList<UploadGroupMemberState> Members,
    bool ReadyForAttachment,
    IReadOnlyList<Guid> OpenMemberReceiptIds,
    IReadOnlyList<Guid> GroupMemberReceiptIds,
    IReadOnlyDictionary<Guid, long> OpenMemberReceiptVersions,
    IReadOnlyDictionary<Guid, long> GroupMemberReceiptVersions,
    bool CanDiscard)
{
    /// <summary>The refusal when the upload is not ready for one attach decision, or the reviewed roster is not the upload's.</summary>
    public const string AttachIncompleteMessage =
        "This submission is incomplete or a file is not ready. No further files were added. Refresh and review every file before trying again.";

    /// <summary>The refusal when the upload may not be discarded now, or the reviewed roster is not the upload's as it stands.</summary>
    public const string DiscardChangedMessage =
        "This submission changed or is still being processed. Refresh and try again.";

    /// <summary>
    /// The whole upload awaits its one decision: every file is ready for one
    /// attach and none is decided yet.
    /// </summary>
    public bool AwaitsDecision => ReadyForAttachment && OpenMemberReceiptIds.Count == Group.Members.Count;

    /// <summary>Each file as Leave reads it: whether its outcome is a possible match, and its receipt.</summary>
    public IReadOnlyList<ImageMatchLeaveFile> LeaveFiles => Members
        .Select(member => new ImageMatchLeaveFile(
            member.Outcome is { Kind: UploadOutcomeKind.PossibleMatch },
            member.Receipt))
        .ToArray();

    /// <summary>
    /// Whether Leave declines the image match on this upload now (R1092): it
    /// awaits its decision as its page's Decision phase reads it, so it is not
    /// discarded and no file still waits on custody.
    /// </summary>
    public bool OffersLeave => ImageMatchLeave.IsOffered(
        AwaitsDecision
            && Group.Discard is null
            && !Members.Any(member => UploadReviewFile.AwaitsCustody(member.Receipt)),
        LeaveFiles);

    /// <summary>Whether the upload was already left, a repeat of which is answered as done (R1130).</summary>
    public bool AlreadyLeft => Group.ImageMatchDeclined is not null;

    /// <summary>
    /// Reads the upload, each member concurrently: its status, its receipt and, once
    /// terminal, its outcome (every store behind these ports is
    /// IDbContextFactory-backed, so the reads share no context). Without an actor no
    /// receipt or outcome is read and nothing may be discarded. Null when no upload
    /// has this identity.
    /// </summary>
    public static async Task<UploadGroupState?> LoadAsync(
        IIntakeSubmissionGroupStore groups,
        IQueuedIntakeStatusQueries statuses,
        IGetIntake intake,
        IUploadOutcomeQueries outcomeQueries,
        Guid groupId,
        ActionActor? actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(outcomeQueries);
        var group = await groups.GetAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var members = await Task.WhenAll(group.Members.Select(async member =>
        {
            var status = await statuses.GetAsync(member.StagedReceiptId, cancellationToken);
            UploadOutcomeView? outcome = null;
            IntakeReceipt? receipt = null;
            if (actor is not null)
            {
                // One receipt read serves both the outcome and the page.
                receipt = await intake.ExecuteAsync(
                    new(status?.ProcessedReceiptId ?? member.StagedReceiptId, actor), cancellationToken);
                if (status is { Status: QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed })
                {
                    outcome = await outcomeQueries.BuildAsync(status, receipt, group.Id, actor, cancellationToken);
                }
            }

            return new UploadGroupMemberState(member, status, receipt, outcome);
        }));

        // A registered image group is reported once but AwaitingInstruction still
        // has one submission-level destination decision. Its Image record's origin
        // is lifecycle data, not the posted receipt for every member.
        var open = members
            .Where(member => member.Outcome is { IsOpenDecision: true, Attach: not null })
            .ToArray();
        var readyForAttachment = group.Channel == IntakeSourceChannel.ManualUpload
            && group.Members.Count == group.ExpectedMemberCount
            && group.Members.Count > 0
            && members.All(member => member.Status is { Status: QueuedIntakeStatusKind.Complete }
                && member.Outcome is not null
                && (member.Outcome.Attach is not null
                    || member.Outcome.Kind is UploadOutcomeKind.Attached or UploadOutcomeKind.Resolved)
                && !member.Outcome.IsStillWorking);
        var groupMemberReceiptIds = members
            .Select(member => member.Status?.ProcessedReceiptId ?? member.Status?.StagedReceiptId)
            .Where(receiptId => receiptId is not null)
            .Select(receiptId => receiptId!.Value)
            .ToArray();

        // Discard is offered only after every member has completed. The
        // serializable store is still authoritative for associations,
        // allocations, registrations, and a changed durable roster.
        var discardVersions = new Dictionary<Guid, long>();
        var canDiscard = false;
        if (actor is not null && group.Discard is null && group.Channel == IntakeSourceChannel.ManualUpload
            && group.Members.Count == group.ExpectedMemberCount
            && members.All(member => member.Status is { Status: QueuedIntakeStatusKind.Complete,
                ProcessedReceiptId: not null } && member.Receipt is not null))
        {
            var receipts = members.Select(member => member.Receipt!).ToArray();
            if (receipts.Select(receipt => receipt.Id).Distinct().Count() == group.Members.Count)
            {
                discardVersions = receipts.ToDictionary(receipt => receipt.Id, receipt => receipt.Version);
                canDiscard = discardVersions.Count == group.Members.Count;
            }
        }

        return new(
            group,
            members,
            readyForAttachment,
            open.Select(member => member.Status!.ProcessedReceiptId ?? member.Status!.StagedReceiptId).ToArray(),
            groupMemberReceiptIds,
            open.ToDictionary(
                member => member.Status!.ProcessedReceiptId ?? member.Status!.StagedReceiptId,
                member => member.Outcome!.Attach!.ReceiptVersion),
            discardVersions,
            canDiscard);
    }

    /// <summary>
    /// The reviewed attach roster in the upload's own order: every member's receipt
    /// with the version it was reviewed at, the still-open members included. A post's
    /// keys can be reordered by its binder, so the upload is the only source of
    /// execution order, and a receipt outside the upload is refused. Completed members
    /// are included because the attach must prove their identical decision before
    /// continuing a partial retry.
    /// </summary>
    public bool TryGetRoster(
        IReadOnlyDictionary<Guid, long>? receiptVersions,
        out IReadOnlyList<Guid> roster)
    {
        roster = [];
        if (receiptVersions is null || receiptVersions.Count == 0
            || receiptVersions.Any(pair => pair.Key == Guid.Empty || pair.Value < 0))
        {
            return false;
        }

        var ordered = GroupMemberReceiptIds
            .Where(receiptVersions.ContainsKey)
            .ToArray();
        if (ordered.Length != receiptVersions.Count || ordered.Length != GroupMemberReceiptIds.Count)
        {
            return false;
        }
        if (OpenMemberReceiptIds.Any(member => !receiptVersions.ContainsKey(member)))
        {
            return false;
        }

        roster = ordered;
        return true;
    }

    /// <summary>Whether a discard names the upload's version and every member's receipt at the version it stands at.</summary>
    public bool HasCurrentDiscardRoster(
        long groupVersion,
        IReadOnlyDictionary<Guid, long> receiptVersions)
    {
        ArgumentNullException.ThrowIfNull(receiptVersions);
        return groupVersion == Group.Version
            && receiptVersions.Count == GroupMemberReceiptVersions.Count
            && receiptVersions.All(item => GroupMemberReceiptVersions.TryGetValue(item.Key, out var current)
                && current == item.Value);
    }
}
