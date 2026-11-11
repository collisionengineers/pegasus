using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// Captures one still image from a confirmed Case video and files it on the
/// Case as a staff-uploaded <see cref="DocumentSemanticRole.Image"/> in the
/// report set (operator, 11 November 2026; R945).
/// </summary>
public sealed record SnapshotCaseVideoCommand(
    Guid CaseId,
    Guid VideoOccurrenceId,
    long TimestampMs,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    ActionActor Actor,
    string OperationKey);

public interface ISnapshotCaseVideo
{
    Task<AddCaseDocumentResult> ExecuteAsync(
        SnapshotCaseVideoCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation, replay-stable occurrence identity, and bounded custody-safe file
/// naming for still frames captured from a Case video (R945).
/// </summary>
public static class VideoSnapshotPolicy
{
    /// <summary>Matches the Case document upload ceiling (25 MiB).</summary>
    public const long MaximumSnapshotBytes = 25L * 1024 * 1024;

    /// <summary>Custody's file-name ceiling, extension included (R895).</summary>
    public const int MaximumFileNameLength = 120;

    /// <summary>
    /// Whether <paramref name="mediaType"/> is a supported Case video format
    /// (<c>video/mp4</c> or <c>video/quicktime</c>), ignoring optional MIME parameters.
    /// </summary>
    public static bool IsSupportedVideoMediaType(string? mediaType)
    {
        var bare = BareMediaType(mediaType);
        return string.Equals(bare, IntakeUploadFilePolicy.Mp4MediaType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(bare, IntakeUploadFilePolicy.MovMediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes <paramref name="mediaType"/> to <c>image/jpeg</c> or
    /// <c>image/png</c>, refusing any other format.
    /// </summary>
    public static string NormalizeImageMediaType(string? mediaType)
    {
        var bare = BareMediaType(mediaType);
        if (string.Equals(bare, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }
        if (string.Equals(bare, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        throw new InvalidOperationException("Only a JPEG or PNG file may have the Image type.");
    }

    public static void Validate(SnapshotCaseVideoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }
        if (command.VideoOccurrenceId == Guid.Empty)
        {
            throw new ArgumentException("A video occurrence identifier is required.", nameof(command));
        }
        if (command.TimestampMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The snapshot timestamp cannot be negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        if (command.OperationKey.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The operation key cannot exceed 100 characters.");
        }
        if (command.Content.IsEmpty)
        {
            throw new ArgumentException("Snapshot content cannot be empty.", nameof(command));
        }
        if (command.Content.Length > MaximumSnapshotBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                $"Snapshot content exceeds the {MaximumSnapshotBytes} byte limit.");
        }

        var mediaType = NormalizeImageMediaType(command.MediaType);
        if (IntakeUploadFilePolicy.HeaderMatches(null, mediaType, command.Content.Span) != true)
        {
            throw new InvalidOperationException(
                "The snapshot content does not match its declared image format.");
        }
    }

    /// <summary>
    /// Deterministic per-operation source occurrence identity so an exact replay
    /// finds the same occurrence while separate snapshots always create distinct
    /// Case documents rather than superseding an earlier frame.
    /// </summary>
    public static string SourceOccurrenceIdentityFor(
        Guid videoOccurrenceId,
        long timestampMs,
        string operationKey)
    {
        if (videoOccurrenceId == Guid.Empty)
        {
            throw new ArgumentException("A video occurrence identifier is required.", nameof(videoOccurrenceId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(timestampMs);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        var keyDigest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(operationKey.Trim())))[..24];
        return string.Create(
            CultureInfo.InvariantCulture,
            $"video-snapshot:{videoOccurrenceId:N}:{timestampMs}:{keyDigest}");
    }

    /// <summary>
    /// Formats a snapshot file name as <c>{video-stem}-frame-{mm}m{ss}s.jpg</c>
    /// (or <c>.png</c>), disambiguating collisions on the Case first with
    /// <c>{ms}ms</c> and then with <c> (2)</c>, <c> (3)</c> while keeping the
    /// full name custody-safe and within <see cref="MaximumFileNameLength"/> (R895, R896, R945).
    /// </summary>
    public static string FileNameFor(
        string? sourceVideoFileName,
        long timestampMs,
        string imageMediaType,
        IEnumerable<string>? existingFileNames = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timestampMs);
        var normalizedType = NormalizeImageMediaType(imageMediaType);
        var extension = IntakeUploadFilePolicy.ExtensionFor(normalizedType);
        var safeStem = SanitizeStem(sourceVideoFileName);

        var taken = existingFileNames is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(existingFileNames, StringComparer.OrdinalIgnoreCase);

        var totalSeconds = timestampMs / 1000;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        var milliseconds = timestampMs % 1000;

        var primaryTail = string.Create(
            CultureInfo.InvariantCulture,
            $"-frame-{minutes:00}m{seconds:00}s{extension}");
        var primary = ComposeBounded(safeStem, primaryTail);
        if (!taken.Contains(primary))
        {
            return primary;
        }

        var msBaseTail = string.Create(
            CultureInfo.InvariantCulture,
            $"-frame-{minutes:00}m{seconds:00}s{milliseconds:000}ms");
        var msCandidate = ComposeBounded(safeStem, msBaseTail + extension);
        if (!taken.Contains(msCandidate))
        {
            return msCandidate;
        }

        var ordinal = 2;
        while (true)
        {
            var numberedTail = string.Create(
                CultureInfo.InvariantCulture,
                $"{msBaseTail} ({ordinal++}){extension}");
            var numbered = ComposeBounded(safeStem, numberedTail);
            if (!taken.Contains(numbered))
            {
                return numbered;
            }
        }
    }

    private static string ComposeBounded(string safeStem, string tail)
    {
        var maxStemLength = Math.Max(1, MaximumFileNameLength - tail.Length);
        var stem = safeStem.Length <= maxStemLength
            ? safeStem
            : safeStem[..maxStemLength].TrimEnd();
        if (string.IsNullOrEmpty(stem))
        {
            stem = "video"[..Math.Min(5, maxStemLength)];
        }

        return stem + tail;
    }

    private static string SanitizeStem(string? sourceVideoFileName)
    {
        var leaf = Path.GetFileName((sourceVideoFileName ?? string.Empty).Replace('\\', '/'));
        var rawStem = Path.GetFileNameWithoutExtension(leaf);
        var safe = new string(rawStem.Select(character =>
            character < ' ' || character is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/'
                ? '_'
                : character).ToArray())
            .Trim()
            .Trim('.');
        return string.IsNullOrWhiteSpace(safe) ? "video" : safe;
    }

    private static string BareMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return string.Empty;
        }

        var trimmed = mediaType.Trim();
        var semicolon = trimmed.IndexOf(';');
        return semicolon < 0 ? trimmed : trimmed[..semicolon].TrimEnd();
    }
}
