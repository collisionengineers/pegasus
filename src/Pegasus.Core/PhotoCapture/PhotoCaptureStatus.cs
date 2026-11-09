namespace Pegasus.Core.PhotoCapture;

/// <summary>Which reminder a link is due, in the words the Links we sent list shows; null when none is due.</summary>
public sealed record DueCaptureReminder(string Code, string Label);

/// <summary>
/// A capture's status and its due reminder, derived from the record each time
/// they are read and never stored (FRD-28). Reminders are never sent by
/// Pegasus: the list shows what is due and a member of staff sends it with
/// one click (operator, 9 November 2026; MAIL-19 stands).
/// </summary>
public static class PhotoCaptureStatus
{
    public static readonly TimeSpan FirstNotOpenedReminder = TimeSpan.FromHours(24);
    public static readonly TimeSpan SecondNotOpenedReminder = TimeSpan.FromHours(48);
    public static readonly TimeSpan UnfinishedReminder = TimeSpan.FromHours(4);
    public static readonly TimeSpan ExpiryWarning = TimeSpan.FromDays(1);

    /// <summary>After this many reminders with nothing back, the link needs a phone call.</summary>
    public const int RemindersBeforeCall = 2;

    public static PhotoCaptureState Evaluate(
        PhotoCapture capture,
        IReadOnlyList<MissingCaptureItem> missing,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(missing);
        if (capture.IsRevoked)
        {
            return PhotoCaptureState.Cancelled;
        }

        var sentOrDone = capture.SentAtUtc is not null;
        if (sentOrDone)
        {
            return missing.Count == 0 ? PhotoCaptureState.Complete : PhotoCaptureState.SomethingMissing;
        }

        if (nowUtc >= capture.ExpiresAtUtc)
        {
            return capture.AcceptedItemCount > 0
                ? missing.Count == 0 ? PhotoCaptureState.Complete : PhotoCaptureState.SomethingMissing
                : PhotoCaptureState.Expired;
        }

        if (capture.ReminderCount >= RemindersBeforeCall
            && capture.LastRemindedAtUtc is { } reminded
            && (capture.LastItemAtUtc is null || capture.LastItemAtUtc < reminded)
            && (capture.OpenedAtUtc is null || capture.OpenedAtUtc < reminded)
            && capture.CallLoggedAtUtc is null)
        {
            return PhotoCaptureState.NeedsCall;
        }

        if (capture.AcceptedItemCount > 0)
        {
            return PhotoCaptureState.PhotosComingIn;
        }

        return capture.OpenedAtUtc is null ? PhotoCaptureState.SentNotOpened : PhotoCaptureState.Opened;
    }

    /// <summary>
    /// The reminder due now, if any, in this order: not opened after 24 hours
    /// and again after 48; started but unfinished after 4 quiet hours;
    /// something missing once sent; about to expire within a day. A capture
    /// that is complete, cancelled, expired or already needs a call is due
    /// nothing.
    /// </summary>
    public static DueCaptureReminder? DueReminder(
        PhotoCapture capture,
        IReadOnlyList<MissingCaptureItem> missing,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(missing);
        var state = Evaluate(capture, missing, nowUtc);
        if (state is PhotoCaptureState.Complete or PhotoCaptureState.Cancelled
            or PhotoCaptureState.Expired or PhotoCaptureState.NeedsCall)
        {
            return null;
        }

        if (state == PhotoCaptureState.SentNotOpened)
        {
            var sentSince = capture.LastRemindedAtUtc ?? capture.CreatedAtUtc;
            var wait = capture.ReminderCount == 0 ? FirstNotOpenedReminder : SecondNotOpenedReminder;
            return nowUtc - sentSince >= wait ? new("not_opened", "Not opened") : null;
        }

        if (state == PhotoCaptureState.SomethingMissing)
        {
            return capture.LastRemindedAtUtc is null || capture.LastRemindedAtUtc < capture.SentAtUtc
                ? new("something_missing", "Something missing")
                : null;
        }

        // The quiet clock starts at the last photo, else the opening, and a
        // reminder since then restarts it.
        var quietSince = capture.LastItemAtUtc ?? capture.OpenedAtUtc ?? capture.CreatedAtUtc;
        if (capture.LastRemindedAtUtc is { } reminded && reminded > quietSince)
        {
            quietSince = reminded;
        }

        if (state is PhotoCaptureState.Opened or PhotoCaptureState.PhotosComingIn
            && nowUtc - quietSince >= UnfinishedReminder)
        {
            return new("unfinished", "Started but not finished");
        }

        if (capture.ExpiresAtUtc - nowUtc <= ExpiryWarning
            && (capture.LastRemindedAtUtc is null || capture.ExpiresAtUtc - capture.LastRemindedAtUtc.Value > ExpiryWarning))
        {
            return new("expiring", "About to expire");
        }

        return null;
    }
}
