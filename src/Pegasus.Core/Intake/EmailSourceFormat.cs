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
