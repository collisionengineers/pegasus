namespace Pegasus.Core.Intake;

/// <summary>
/// The one rule for "this file is an email". The intake reader detects the
/// format with it and the Case page routes the file to the Correspondence tab
/// with it, so neither holds a second copy of the extension or media type.
/// </summary>
public static class EmailSourceFormat
{
    public const string MediaType = "message/rfc822";

    public static bool IsEmail(string? fileName, string? mediaType) =>
        (fileName is not null
            && Path.GetExtension(fileName).Equals(".eml", StringComparison.OrdinalIgnoreCase))
        || (mediaType is not null
            && mediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase));
}
