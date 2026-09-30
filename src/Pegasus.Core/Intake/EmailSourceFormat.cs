namespace Pegasus.Core.Intake;

/// <summary>
/// The one rule for "this file is an email". The intake reader detects the
/// format with it and the Case page routes the file to the Correspondence tab
/// with it, so neither holds a second copy of the extension or media type.
/// </summary>
public static class EmailSourceFormat
{
    public const string MediaType = "message/rfc822";

    /// <summary>An Outlook message, which the reader opens as well.</summary>
    public const string OutlookMediaType = "application/vnd.ms-outlook";

    /// <summary>The name of a retained message that has no subject.</summary>
    public const string UntitledMessageFileName = "Message.eml";

    /// <summary>
    /// The longest subject kept in a name. With <c>.eml</c> it stays inside every
    /// custody name limit, so a long subject never refuses a message.
    /// </summary>
    public const int MaximumNameSubjectLength = 100;

    // The Windows-invalid superset, fixed so a name is the same on every host
    // and valid in every store the file reaches, and the quote, apostrophe and
    // semicolon the source download strips from a name, so the stored name and
    // the downloaded name are the same.
    private const string RefusedNameCharacters = "\"'<>|:*?;\\/";

    // The names Windows reserves for devices. A file called CON.eml (or CON.txt.eml,
    // or "con. ") opens or fails as the device, so such a stem is never used.
    private static readonly HashSet<string> ReservedDeviceNames = new(
        new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }
            .Concat("123456789¹²³".SelectMany(digit => new[] { "COM" + digit, "LPT" + digit })),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The one name for a retained mail message: its subject, trimmed, without
    /// the characters a file name refuses and cut to
    /// <see cref="MaximumNameSubjectLength"/>, then <c>.eml</c>. The subject never
    /// ends in a dot or a space before the extension. A message with no usable
    /// subject, or whose subject is a Windows device name (<c>CON</c>, <c>NUL</c>,
    /// <c>COM1</c> and the rest, in any case, with or without an extension), is
    /// <see cref="UntitledMessageFileName"/>. The name says what the message is
    /// about. It is not unique: the receipt is the identity.
    /// </summary>
    public static string RetainedMessageFileName(string? subject)
    {
        var kept = new string((subject ?? string.Empty)
            .Where(character => !char.IsControl(character)
                && !RefusedNameCharacters.Contains(character))
            .ToArray()).TrimStart();
        if (kept.Length > MaximumNameSubjectLength)
        {
            var length = char.IsHighSurrogate(kept[MaximumNameSubjectLength - 1])
                ? MaximumNameSubjectLength - 1
                : MaximumNameSubjectLength;
            kept = kept[..length];
        }
        kept = TrimStemEnd(kept);

        return kept.Length == 0 || IsReservedDeviceName(kept)
            ? UntitledMessageFileName
            : kept + ".eml";
    }

    // Windows strips a trailing dot or space from a name, so neither may end the
    // stem that ".eml" is added to.
    private static string TrimStemEnd(string value)
    {
        var end = value.Length;
        while (end > 0 && (value[end - 1] == '.' || char.IsWhiteSpace(value[end - 1])))
        {
            end--;
        }

        return value[..end];
    }

    // The device name is what precedes the first dot, less any trailing spaces:
    // "NUL.txt" and "nul .txt" are the NUL device as much as "NUL" is.
    private static bool IsReservedDeviceName(string stem)
    {
        var dot = stem.IndexOf('.');
        return ReservedDeviceNames.Contains((dot < 0 ? stem : stem[..dot]).TrimEnd());
    }

    public static bool IsEmail(string? fileName, string? mediaType) =>
        (fileName is not null
            && Path.GetExtension(fileName).Equals(".eml", StringComparison.OrdinalIgnoreCase))
        || (mediaType is not null
            && mediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase));

    /// <summary>An email of either format: a MIME message or an Outlook message.</summary>
    public static bool IsMailMessage(string? fileName, string? mediaType) =>
        IsEmail(fileName, mediaType)
        || (fileName is not null
            && Path.GetExtension(fileName).Equals(".msg", StringComparison.OrdinalIgnoreCase))
        || (mediaType is not null
            && mediaType.Equals(OutlookMediaType, StringComparison.OrdinalIgnoreCase));
}
