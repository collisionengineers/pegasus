namespace Pegasus.Core.PhotoCapture;

/// <summary>Which reminder a link is due, in the words the Links we sent list shows; null when none is due.</summary>
public sealed record DueCaptureReminder(string Code, string Label);

/// <summary>
/// One list row with what is derived from it as it is read (FRD-28: never
/// stored), the same for the Photo capture pages and the photo link tools
/// (#2130; R1092). <see cref="IsLive"/> is whether the link still takes
/// photos, which Resend needs (Core refuses it on a dead link); a link whose
/// Case is archived is dead (R793).
/// </summary>
public sealed record PhotoCaptureRowStatus(
    PhotoCaptureListRow Source,
    PhotoCaptureState State,
    DueCaptureReminder? Due,
    bool IsLive)
{
    public PhotoCapture Capture => Source.Capture;

    /// <summary>The state and due reminder a row derives, from its own record and missing items.</summary>
    public static PhotoCaptureRowStatus For(PhotoCaptureListRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new(
            row,
            PhotoCaptureStatus.Evaluate(row.Capture, PhotoCaptureStatus.MissingOf(row), row.Photographs, now, row.CaseArchived),
            PhotoCaptureStatus.DueReminder(row, now),
            PhotoCaptureAccess.IsLive(row.Capture, now, row.CaseArchived));
    }
}

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

    /// <param name="photographs">
    /// How many of the capture's items are photographs. Paperwork or video
    /// alone, such as a hand-off's staff phone sending the form's documents,
    /// is not photos coming in.
    /// </param>
    /// <param name="caseArchived">
    /// Whether the capture's Case is archived: its link is dead and the
    /// capture reads Expired (operator, 9 November 2026; R793).
    /// </param>
    public static PhotoCaptureState Evaluate(
        PhotoCapture capture,
        IReadOnlyList<MissingCaptureItem> missing,
        int photographs,
        DateTimeOffset nowUtc,
        bool caseArchived = false)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(missing);
        if (capture.IsRevoked)
        {
            return PhotoCaptureState.Cancelled;
        }

        if (caseArchived)
        {
            return PhotoCaptureState.Expired;
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

        if (photographs > 0)
        {
            return PhotoCaptureState.PhotosComingIn;
        }

        return capture.OpenedAtUtc is null ? PhotoCaptureState.SentNotOpened : PhotoCaptureState.Opened;
    }

    /// <summary>
    /// Whether photos the capture received are still to settle: an item
    /// arrived after the last settle, or before any. A standing or website
    /// capture reads Uploading while it is, and Received once it has settled,
    /// and is not graded against the guided checklist (operator, 11 November
    /// 2026; v51 Q; R948).
    /// </summary>
    public static bool IsUploading(PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return capture.LastItemAtUtc is { } last && (capture.SettledAtUtc is not { } settled || settled < last);
    }

    /// <summary>
    /// Whether a list row falls in one of the Direct links tab's workflow
    /// scopes (operator, 11 November 2026; v51 N; R948): Awaiting is a live
    /// link not yet complete; Needs chase is a live link with a reminder due,
    /// or one that needs a call; Closed is a dead link that is not complete.
    /// </summary>
    public static bool InScope(PhotoCaptureScope scope, PhotoCaptureState state, DueCaptureReminder? due, bool isLive) =>
        scope switch
        {
            PhotoCaptureScope.Awaiting => isLive && state != PhotoCaptureState.Complete,
            PhotoCaptureScope.NeedsChase => (isLive && due is not null) || state == PhotoCaptureState.NeedsCall,
            PhotoCaptureScope.Complete => state == PhotoCaptureState.Complete,
            PhotoCaptureScope.Closed => !isLive && state != PhotoCaptureState.Complete,
            _ => true
        };

    /// <summary>
    /// A list row's due reminder: the capture's own due reminder
    /// over the row's own missing items, photo count and Case. Only a link HQ
    /// sent from a Case has one.
    /// </summary>
    public static DueCaptureReminder? DueReminder(PhotoCaptureListRow row, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Capture.Origin == PhotoCaptureOrigin.CaseLink
            ? DueReminder(row.Capture, MissingOf(row), row.Photographs, nowUtc, row.CaseArchived)
            : null;
    }

    /// <summary>A list row's missing items as the evaluation takes them: its labels are its codes.</summary>
    public static IReadOnlyList<MissingCaptureItem> MissingOf(PhotoCaptureListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [.. row.MissingItems.Select(label => new MissingCaptureItem(label, label))];
    }

    /// <summary>
    /// The reminder due now, if any, in this order: not opened after 24 hours
    /// and again after 48; started but unfinished after 4 quiet hours;
    /// something missing once sent; about to expire within a day. A capture
    /// that is complete, cancelled, expired or already needs a call is due
    /// nothing, and neither is one whose Case is archived (R793).
    /// </summary>
    public static DueCaptureReminder? DueReminder(
        PhotoCapture capture,
        IReadOnlyList<MissingCaptureItem> missing,
        int photographs,
        DateTimeOffset nowUtc,
        bool caseArchived = false)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(missing);
        var state = Evaluate(capture, missing, photographs, nowUtc, caseArchived);
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
