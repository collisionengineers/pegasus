using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// What the store reads for one attachment's text: the message's attachment
/// count, the named attachment's file name and media type, and that
/// attachment's search-document text, trimmed and cut at one character past
/// the caller's cap so a longer text shows as such.
/// </summary>
public sealed record RetainedMailAttachmentTextRead(
    int AttachmentCount,
    string? FileName,
    string? MediaType,
    string? Text);

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
public sealed class GetRetainedMailAttachmentText(IRetainedMailQueries queries)
{
    public const int MaximumCharacters = 20_000;

    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

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
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A retained message identifier is required.",
                nameof(messageId));
        }

        var read = await queries.ReadAttachmentTextAsync(
            messageId, attachmentOrdinal, maximumCharacters, cancellationToken);
        if (read is null)
        {
            return null;
        }

        if (attachmentOrdinal >= read.AttachmentCount
            || read.FileName is not { } fileName
            || read.MediaType is not { } mediaType)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachmentOrdinal),
                attachmentOrdinal,
                $"The message has {read.AttachmentCount} attachment(s); positions count from 0.");
        }

        var text = string.IsNullOrWhiteSpace(read.Text) ? null : read.Text;
        var isTruncated = text is not null && text.Length > maximumCharacters;
        return new(
            attachmentOrdinal,
            fileName,
            mediaType,
            isTruncated ? text![..maximumCharacters] : text,
            isTruncated);
    }
}
