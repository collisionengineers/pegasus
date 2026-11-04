using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The search documents the intake reader produced for one receipt (FRD-08):
/// the body row, then one row per attachment by the attachment's ordinal.
/// Read-only; the projection itself is written by intake.
/// </summary>
public interface IIntakeSearchDocumentReader
{
    Task<IReadOnlyList<IntakeSearchDocument>> ReadAsync(Guid intakeReceiptId, CancellationToken cancellationToken);
}

/// <summary>
/// One attachment's extracted text, cut at the caller's cap. Text is null when
/// the reader produced none for it (an image, or a scan not read).
/// </summary>
public sealed record RetainedMailAttachmentText(
    int AttachmentOrdinal,
    string FileName,
    string MediaType,
    string? Text,
    bool IsTruncated);

/// <summary>
/// Reads the text the intake reader extracted from one attachment of a
/// retained message, for a caseworker or the Automation actor: a PDF's text
/// layer or a Word document's text, never the bytes. The attachment is named
/// by its position in the message's attachment list, which is the ordinal the
/// search projection keys on. Nothing is written.
/// </summary>
public sealed class GetRetainedMailAttachmentText(
    GetRetainedMail getRetainedMail,
    IIntakeSearchDocumentReader documents)
{
    public const int MaximumCharacters = 20_000;

    private readonly GetRetainedMail getRetainedMail =
        getRetainedMail ?? throw new ArgumentNullException(nameof(getRetainedMail));
    private readonly IIntakeSearchDocumentReader documents =
        documents ?? throw new ArgumentNullException(nameof(documents));

    /// <returns>Null when the message is not retained.</returns>
    public async Task<RetainedMailAttachmentText?> ExecuteAsync(
        ActionActor actor,
        Guid messageId,
        int attachmentOrdinal,
        int maximumCharacters = MaximumCharacters,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentOutOfRangeException.ThrowIfNegative(attachmentOrdinal);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCharacters, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCharacters, MaximumCharacters);

        var detail = await getRetainedMail.ExecuteAsync(actor, messageId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        if (attachmentOrdinal >= detail.Attachments.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachmentOrdinal),
                attachmentOrdinal,
                $"The message has {detail.Attachments.Count} attachment(s); positions count from 0.");
        }

        var attachment = detail.Attachments[attachmentOrdinal];
        var text = detail.Summary.IntakeReceiptId is { } receiptId
            ? (await documents.ReadAsync(receiptId, cancellationToken))
                .FirstOrDefault(document => document.AttachmentOrdinal == attachmentOrdinal)
                ?.Text
            : null;
        text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var isTruncated = text is not null && text.Length > maximumCharacters;
        return new(
            attachmentOrdinal,
            attachment.FileName,
            attachment.MediaType,
            isTruncated ? text![..maximumCharacters] : text,
            isTruncated);
    }
}
