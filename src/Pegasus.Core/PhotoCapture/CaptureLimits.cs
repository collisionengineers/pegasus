using Pegasus.Core.Intake;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// The bounds of the capture surface (FRD-28). Constants, not configuration:
/// the removed upload links had seventeen configuration keys that failed
/// lazily, and nothing here varies by deployment. No per-file figure may
/// exceed the intake per-file cap, which <see cref="IntakeEnvelopeLimits"/>
/// owns and which ingress may tighten and never raise.
/// </summary>
public static class CaptureLimits
{
    /// <summary>One request body: a compressed photo, or one part of a larger file.</summary>
    public const int SingleRequestBytes = 4 * 1024 * 1024;

    /// <summary>Every part of a staged upload but the last.</summary>
    public const int PartBytes = SingleRequestBytes;

    public const int PhotoBytes = 10_000_000;
    public const int OriginalBytes = 25_000_000;
    public const int DocumentBytes = 25_000_000;
    public const int VideoBytes = IntakeEnvelopeLimits.MaximumContentLength;

    /// <summary>The typed details body.</summary>
    public const int DetailsBytes = 16 * 1024;

    public const int MaximumPhotosPerCapture = 250;
    public const int MaximumOriginalsPerCapture = 250;
    public const int MaximumDocumentsPerCapture = 20;
    public const int MaximumVideosPerCapture = 5;
    public const long MaximumBytesPerCapture = 1_500_000_000;

    /// <summary>Captures one standing link may start in a day: the website link, and a contact's.</summary>
    public const int WebsiteMintsPerDay = 100;
    public const int ContactMintsPerDay = 50;

    public const int NoteLength = 2000;
    public const int NameLength = 200;
    public const int MobileLength = 40;
    public const int EmailLength = 320;
    public const int FreeTextLength = 500;
    public const int AreaLength = 10;

    /// <summary>The lifetimes a link may be given, in days; the middle one is the default (handover, 5 November 2026).</summary>
    public static IReadOnlyList<int> LifetimeDays { get; } = [3, 7, 14];

    public const int DefaultLifetimeDays = 7;

    public static bool IsLifetime(int days) => LifetimeDays.Contains(days);

    /// <summary>The largest single file of a kind, which the admission filter sets before it reads a body.</summary>
    public static long MaximumBytesFor(PhotoCaptureItemKind kind, bool original) => kind switch
    {
        PhotoCaptureItemKind.Video => VideoBytes,
        PhotoCaptureItemKind.Paperwork => DocumentBytes,
        _ when original => OriginalBytes,
        _ => PhotoBytes
    };

    public static int PartCount(long totalBytes) =>
        (int)((totalBytes + PartBytes - 1) / PartBytes);

    public static int PartLength(long totalBytes, int partNumber) =>
        partNumber < PartCount(totalBytes)
            ? PartBytes
            : (int)(totalBytes - (long)(PartCount(totalBytes) - 1) * PartBytes);
}
