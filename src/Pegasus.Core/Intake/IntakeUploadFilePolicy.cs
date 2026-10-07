namespace Pegasus.Core.Intake;

/// <summary>
/// The file types the intake reader opens, and the check video file names,
/// media types and bounded content must pass before intake. Other retained
/// sources are classified by the intake reader, including unsupported
/// material.
/// </summary>
public static class IntakeUploadFilePolicy
{
    public const string Mp4MediaType = "video/mp4";
    public const string MovMediaType = "video/quicktime";

    /// <summary>
    /// Every file type the intake reader opens, by extension, with the media
    /// type it is read as. The first extension of a media type is the one a
    /// nameless file is given.
    /// </summary>
    private static readonly (string Extension, string MediaType)[] SupportedTypes =
    [
        (".pdf", "application/pdf"),
        (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        (".doc", "application/msword"),
        (".msg", EmailSourceFormat.OutlookMediaType),
        (".eml", EmailSourceFormat.MediaType),
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".png", "image/png"),
        (".mp4", Mp4MediaType),
        (".mov", MovMediaType)
    ];

    /// <summary>The media type a file name's extension is read as, or null when the reader does not open it.</summary>
    public static string? MediaTypeFor(string fileName) =>
        SupportedTypes
            .FirstOrDefault(type => string.Equals(
                type.Extension,
                Path.GetExtension(fileName),
                StringComparison.OrdinalIgnoreCase))
            .MediaType;

    /// <summary>The extension a file of this media type is named with, or an empty string when the reader does not open it.</summary>
    public static string ExtensionFor(string mediaType) =>
        SupportedTypes
            .FirstOrDefault(type => string.Equals(type.MediaType, mediaType, StringComparison.OrdinalIgnoreCase))
            .Extension
            ?? string.Empty;

    public static bool IsAccepted(string? fileName, string? mediaType, ReadOnlySpan<byte> content)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(mediaType)
            || content.IsEmpty)
        {
            return false;
        }

        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var type = mediaType.Trim().ToLowerInvariant();
        if (extension == ".mp4" || type == Mp4MediaType)
        {
            return extension == ".mp4" && type == Mp4MediaType
                && HasIsoBaseMediaHeader(content, isQuickTime: false);
        }
        if (extension == ".mov" || type == MovMediaType)
        {
            return extension == ".mov" && type == MovMediaType
                && HasIsoBaseMediaHeader(content, isQuickTime: true);
        }
        return true;
    }

    private static bool HasIsoBaseMediaHeader(ReadOnlySpan<byte> content, bool isQuickTime)
    {
        if (content.Length < 16 || !content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        var brands = content.Slice(8, Math.Min(content.Length - 8, 32));
        for (var index = 0; index <= brands.Length - 4; index += 4)
        {
            var brand = brands.Slice(index, 4);
            if (isQuickTime
                ? brand.SequenceEqual("qt  "u8)
                : brand.SequenceEqual("isom"u8)
                    || brand.SequenceEqual("mp42"u8)
                    || brand.SequenceEqual("avc1"u8))
            {
                return true;
            }
        }

        return false;
    }
}
