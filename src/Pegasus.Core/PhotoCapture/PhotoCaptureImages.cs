namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// Whether a capture's photos complete its Case's images (FRD-28): once per
/// capture, only while the Case's images are not complete, and only on a Case
/// that takes a system completion. A capture that has already completed them
/// never completes them again, whether its later photos arrive on a second
/// Send or in a later record of the same capture that merges into the Case
/// (operator, 9 November 2026; R792; 10 November 2026; R841). A record no capture made
/// has no capture marker and completes them as any merge does.
/// </summary>
public static class PhotoCaptureImages
{
    public static bool Completes(
        DateTimeOffset? captureImagesCompletedAtUtc,
        bool caseImagesComplete,
        bool takesSystemCompletion) =>
        captureImagesCompletedAtUtc is null && !caseImagesComplete && takesSystemCompletion;
}
