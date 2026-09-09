namespace Pegasus.Core.Intake;

/// <summary>
/// Validates video file names, media types and bounded content before intake.
/// Other retained sources are classified by the intake reader, including
/// unsupported material. Public links also enforce their allowed-media list.
/// </summary>
public static class IntakeUploadFilePolicy
{
    public const string Mp4MediaType = "video/mp4";
    public const string MovMediaType = "video/quicktime";

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

    public static bool IsVideo(string? mediaType) =>
        string.Equals(mediaType, Mp4MediaType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, MovMediaType, StringComparison.OrdinalIgnoreCase);

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
