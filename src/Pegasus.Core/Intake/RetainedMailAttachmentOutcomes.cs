using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

/// <summary>What became of one attachment of a retained message (message planning, 13 September).</summary>
public enum AttachmentOutcomeKind
{
    NotYetProcessed,
    CaseCreated,
    LinkedToCase,
    VehicleImages,
    CouldNotBeRead,
    ProcessingFailed,
    Triage,
    Unidentified,

    /// <summary>Processed, with no destination of its own to name (the message's classification says the rest).</summary>
    NoDestination
}

public enum AttachmentOutcomeRecordKind
{
    Case,
    ImageIntake,
    Unidentified
}

/// <summary>The record an attachment's outcome opens.</summary>
public sealed record AttachmentOutcomeRecord(AttachmentOutcomeRecordKind Kind, Guid Id, string Reference);

/// <param name="AssetId">The retained attachment asset on the message's receipt.</param>
/// <param name="Reason">Why it could not be read or failed, in the recorded words.</param>
public sealed record RetainedMailAttachmentOutcome(
    Guid AssetId,
    string FileName,
    AttachmentOutcomeKind Kind,
    AttachmentOutcomeRecord? Record,
    string? Reason);

/// <summary>
/// The facts one attachment's outcome is decided from: the receipt that
/// processed it (the message's own receipt, or for a direct image attachment
/// its member receipt in the message's image submission group), the image
/// record and Unidentified item that receipt or its group produced, and whether
/// processing flagged the member as unreadable.
/// </summary>
public sealed record AttachmentOutcomeFacts(
    IntakeReceipt? Receipt,
    AttachmentOutcomeRecord? ImageRecord,
    UnidentifiedItem? Unidentified,
    bool MemberCouldNotBeRead);

/// <summary>The one rule for what an attachment's outcome reads as.</summary>
public static class RetainedMailAttachmentOutcomePolicy
{
    /// <summary>
    /// Could not be read first (an unreadable file travels with its group, but
    /// the person still needs to know this file was not read), then the Case it
    /// reached, a processing failure, Vehicle images, Triage, and Unidentified.
    /// </summary>
    public static (AttachmentOutcomeKind Kind, AttachmentOutcomeRecord? Record, string? Reason) Decide(AttachmentOutcomeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Receipt is not { } receipt)
        {
            return (AttachmentOutcomeKind.NotYetProcessed, null, null);
        }

        var unidentified = facts.Unidentified is { } item
            ? new AttachmentOutcomeRecord(AttachmentOutcomeRecordKind.Unidentified, item.Id, item.Reference)
            : null;
        if (facts.MemberCouldNotBeRead
            || receipt.Decision is IntakeDecision.Unsupported or IntakeDecision.OcrRequired)
        {
            return (AttachmentOutcomeKind.CouldNotBeRead, unidentified, ReasonOf(receipt, facts.Unidentified));
        }

        if (receipt.CurrentCaseId is { } caseId)
        {
            return (
                receipt.AcceptedCaseId == caseId ? AttachmentOutcomeKind.CaseCreated : AttachmentOutcomeKind.LinkedToCase,
                new(AttachmentOutcomeRecordKind.Case, caseId, receipt.CurrentCaseReference ?? string.Empty),
                null);
        }

        if (receipt.Decision == IntakeDecision.TechnicalFailure)
        {
            return (AttachmentOutcomeKind.ProcessingFailed, unidentified, ReasonOf(receipt, facts.Unidentified));
        }

        if (facts.ImageRecord is { } image)
        {
            return (AttachmentOutcomeKind.VehicleImages, image, null);
        }

        if (ProcessIntake.IsTriageRequest(receipt))
        {
            return (AttachmentOutcomeKind.Triage, null, null);
        }

        return unidentified is not null
            ? (AttachmentOutcomeKind.Unidentified, unidentified, null)
            : (AttachmentOutcomeKind.NoDestination, null, null);
    }

    private static string? ReasonOf(IntakeReceipt receipt, UnidentifiedItem? unidentified) =>
        FirstText(receipt.FailureReason, unidentified?.SafeDetail, receipt.DecisionReason);

    private static string? FirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public interface IGetRetainedMailAttachmentOutcomes
{
    /// <summary>Each attachment asset of the message's receipt, with its own outcome.</summary>
    Task<IReadOnlyList<RetainedMailAttachmentOutcome>> ExecuteAsync(
        ActionActor actor,
        Guid intakeReceiptId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-attachment outcomes for a retained message. A message is processed as
/// one receipt, so a document attachment's outcome is that receipt's; a direct
/// image attachment is resubmitted as a member of the message's image
/// submission group (<see cref="SubmitMailboxImageIntake"/>), so its outcome is
/// read from its own member receipt and the group's destination.
/// </summary>
public sealed class GetRetainedMailAttachmentOutcomes(
    IIntakeReceiptQueries receipts,
    IIntakeSubmissionGroupStore submissionGroups,
    IImageIntakeQueries imageIntakes,
    IUnidentifiedStore unidentifiedStore) : IGetRetainedMailAttachmentOutcomes
{
    public async Task<IReadOnlyList<RetainedMailAttachmentOutcome>> ExecuteAsync(
        ActionActor actor,
        Guid intakeReceiptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(intakeReceiptId));
        }

        if (await receipts.GetAsync(intakeReceiptId, cancellationToken) is not { } receipt)
        {
            return [];
        }

        var messageFacts = new AttachmentOutcomeFacts(
            receipt,
            await ImageRecordAsync(imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken)),
            await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken),
            MemberCouldNotBeRead: false);

        var images = SubmitMailboxImageIntake.SelectAttachments(receipt);
        var group = images.Count == 0
            ? null
            : await submissionGroups.FindAsync(
                IntakeSourceChannel.Mailbox,
                SubmitMailboxImageIntake.SubmissionToken(receipt.Id),
                cancellationToken);
        var groupImage = group is null
            ? null
            : await ImageRecordAsync(imageIntakes.GetBySubmissionGroupAsync(group.Id, cancellationToken));
        var groupUnidentified = group is null
            ? null
            : await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.SubmissionGroup(group.Id), cancellationToken);

        // The group's member ordinal is the image attachment's position in the submission.
        var ordinals = images
            .Select((image, ordinal) => (image.Id, ordinal))
            .ToDictionary(entry => entry.Id, entry => entry.ordinal);
        var outcomes = new List<RetainedMailAttachmentOutcome>();
        foreach (var asset in receipt.AssetRecords.Where(asset => asset.Kind == IntakeAssetKind.Attachment))
        {
            var facts = group is not null && ordinals.TryGetValue(asset.Id, out var ordinal)
                ? await MemberFactsAsync(group, ordinal, groupImage, groupUnidentified, cancellationToken)
                : messageFacts;
            var (kind, record, reason) = RetainedMailAttachmentOutcomePolicy.Decide(facts);
            outcomes.Add(new(asset.Id, asset.FileName, kind, record, reason));
        }

        return outcomes;
    }

    private async Task<AttachmentOutcomeFacts> MemberFactsAsync(
        IntakeSubmissionGroup group,
        int ordinal,
        AttachmentOutcomeRecord? groupImage,
        UnidentifiedItem? groupUnidentified,
        CancellationToken cancellationToken)
    {
        var member = group.Members.FirstOrDefault(item => item.Ordinal == ordinal);
        if (member?.ProcessedReceiptId is not { } memberReceiptId
            || await receipts.GetAsync(memberReceiptId, cancellationToken) is not { } memberReceipt)
        {
            return new(null, null, null, false);
        }

        return new(
            memberReceipt,
            groupImage ?? await ImageRecordAsync(imageIntakes.GetByOriginReceiptAsync(memberReceipt.Id, cancellationToken)),
            groupUnidentified
                ?? await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(memberReceipt.Id), cancellationToken),
            member.CouldNotBeRead == true);
    }

    private static async Task<AttachmentOutcomeRecord?> ImageRecordAsync(Task<ImageIntakeDetail?> read) =>
        await read is { } detail
            ? new AttachmentOutcomeRecord(AttachmentOutcomeRecordKind.ImageIntake, detail.Record.Id, detail.Record.ImageIntakeReference)
            : null;
}
