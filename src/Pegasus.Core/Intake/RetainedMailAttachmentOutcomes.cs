using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

/// <summary>What became of a retained message and its attachments (message planning, 13 September).</summary>
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

/// <summary>The record a message's outcome opens.</summary>
public sealed record AttachmentOutcomeRecord(AttachmentOutcomeRecordKind Kind, Guid Id, string Reference);

/// <summary>The one outcome of a retained message, said once for the message.</summary>
/// <param name="Reason">Why it could not be read or failed, in the recorded words.</param>
public sealed record RetainedMailOutcome(
    AttachmentOutcomeKind Kind,
    AttachmentOutcomeRecord? Record,
    string? Reason);

/// <summary>
/// The facts a message's outcome is decided from: the receipt that
/// processed it (the message's own receipt), the image
/// record and Unidentified item that receipt or its group produced, and whether
/// processing flagged the member as unreadable.
/// </summary>
public sealed record AttachmentOutcomeFacts(
    IntakeReceipt? Receipt,
    AttachmentOutcomeRecord? ImageRecord,
    UnidentifiedItem? Unidentified,
    bool MemberCouldNotBeRead);

/// <summary>The one rule for what a message's outcome reads as.</summary>
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

public interface IGetRetainedMailOutcome
{
    /// <summary>
    /// The message's one outcome, decided from its receipt; null when the
    /// receipt is not found. A caller that already holds the receipt passes it
    /// as <paramref name="receipt"/> to spare a second read; it is used only
    /// when it is the receipt asked for.
    /// </summary>
    Task<RetainedMailOutcome?> ExecuteAsync(
        ActionActor actor,
        Guid intakeReceiptId,
        IntakeReceipt? receipt = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a retained message. A message is processed as one receipt,
/// so photographs, PDFs and other document attachments share that receipt's
/// destination, and the outcome is said once for the message.
/// </summary>
public sealed class GetRetainedMailOutcome(
    IIntakeReceiptQueries receipts,
    IImageIntakeQueries imageIntakes,
    IUnidentifiedStore unidentifiedStore) : IGetRetainedMailOutcome
{
    public async Task<RetainedMailOutcome?> ExecuteAsync(
        ActionActor actor,
        Guid intakeReceiptId,
        IntakeReceipt? receipt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(intakeReceiptId));
        }

        receipt = receipt?.Id == intakeReceiptId
            ? receipt
            : await receipts.GetAsync(intakeReceiptId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        // A receipt that was read and has reached a Case states that Case
        // whatever else it produced (the policy's order), so its image record
        // and Unidentified item are read only where it has not.
        var reachedCase = receipt.CurrentCaseId is not null
            && receipt.Decision is not (IntakeDecision.Unsupported or IntakeDecision.OcrRequired);
        var messageFacts = new AttachmentOutcomeFacts(
            receipt,
            reachedCase
                ? null
                : await ImageRecordAsync(imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken)),
            reachedCase
                ? null
                : await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken),
            MemberCouldNotBeRead: false);
        var (kind, record, reason) = RetainedMailAttachmentOutcomePolicy.Decide(messageFacts);
        return new(kind, record, reason);
    }

    private static async Task<AttachmentOutcomeRecord?> ImageRecordAsync(Task<ImageIntakeDetail?> read) =>
        await read is { } detail
            ? new AttachmentOutcomeRecord(AttachmentOutcomeRecordKind.ImageIntake, detail.Record.Id, detail.Record.ImageIntakeReference)
            : null;
}
