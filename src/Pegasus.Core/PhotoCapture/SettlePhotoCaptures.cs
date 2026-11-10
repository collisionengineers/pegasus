using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.Triage;

namespace Pegasus.Core.PhotoCapture;

public sealed record SettlePhotoCapturesResult(int Examined, int Settled, int ReachedCase, int Failures);

/// <summary>
/// The Worker's settle (FRD-28). A capture's open batch settles once it holds
/// a photo and the sender has pressed Send, or the link has expired or was
/// cancelled (operator, 9 November 2026; R792, R794).
/// <para>
/// A batch on a Case waits until every photo has filed on the Case, or has
/// ended for good without filing (<see cref="PhotoCaptureFiling"/>). When at
/// least one photo filed, the Case-side work runs: the Case's images complete
/// once per capture, the sender's note is written once (a changed note is
/// appended), the empty fields fill. The close then commits the batch and,
/// in its own transaction, records the Engineer's bell as owed; the bell
/// rings after and is cleared once rung, so it is never lost. A batch with no
/// photo filed, and one whose Case is archived (R793), closes with nothing
/// written. A Vehicle images batch waits until every photo is evaluated and
/// is then matched again with the link's Principal and the typed accident
/// date (R791): one eligible Case takes the batch straight onto that Case,
/// with no group and no record; none or several close the batch as one
/// submission group of its evaluated photographs, carrying the typed
/// registration and date, whose first member is re-driven to register the
/// record. Each batch whose record later merges into a Case finishes there
/// on its own: the note and the fill, its own kept paperwork and video filed
/// as Other (R803), and the bell owed and rung once (R804).
/// </para>
/// <para>
/// Every step is safe to repeat: the close and the reach are recorded under
/// the capture's lock and say when they already were; the images, the note,
/// the fill and each link are keyed; a re-driven receipt replays its
/// pipeline. Owed bells and reaches run before the walk, each with its own
/// bound, so the waiting captures never starve them under the job's budget.
/// </para>
/// </summary>
public sealed class SettlePhotoCaptures(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks,
    RoutePhotoCapture routing,
    IIntakeMutationStore mutations,
    IProcessQueuedIntake processQueuedIntake,
    IPhotoCaptureCaseWriter caseWriter,
    IAddCaseNote addCaseNote,
    AddTriageNote addTriageNote,
    ICaseStaffNotifier notifier,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How long an item with no receipt yet holds its batch open: an upload
    /// still on its way. An older one never reached intake, and its retry
    /// joins the next batch.
    /// </summary>
    public static readonly TimeSpan UploadInFlightGrace = TimeSpan.FromMinutes(5);

    /// <summary>The Case history's reason for images a photo link completed (operator, 10 November 2026; R871).</summary>
    private const string ImagesReason =
        "The photo link's photos are in: the sender pressed Send, or the link ran out or was cancelled with photos on it.";

    /// <summary>The words the sender's note is written with, on a Case and on a Triage Case alike (R845).</summary>
    private const string CustomerNotePrefix = "Customer's note (photo link): ";

    /// <summary>A Triage Case takes the note as at most this many Triage notes (operator, 10 November 2026; R845).</summary>
    private const int MaximumTriageNotes = 4;

    private static readonly ActionActor SettleActor = ActionActor.SystemWorker(PhotoCaptureIntakeRules.SettleActorId);

    public async Task<SettlePhotoCapturesResult> ExecuteAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var cutoff = IntakeEscapeWindow.LatestClosedStart(now);

        // The bells earlier runs owed and the reaches first, each bounded on
        // its own, then the walk; the reaches and the walk's closes owe bells
        // this run rings at its end. A run that settled and reached nothing
        // owes none, so it does not read them again: the settle runs every
        // minute, and an idle run is three small reads (R838).
        var failures = await RingOwedAsync(maximumItems, cancellationToken);
        var (reached, reachFailures) = await ReachAsync(maximumItems, now, cutoff, cancellationToken);
        var (examined, settled, walkFailures) = await WalkAsync(maximumItems, now, cutoff, cancellationToken);
        failures += reachFailures + walkFailures;
        if (settled > 0 || reached > 0)
        {
            failures += await RingOwedAsync(maximumItems, cancellationToken);
        }

        return new(examined, settled, reached, failures);
    }

    /// <summary>Rings each owed bell, then clears it; a failure leaves it owed for the next run.</summary>
    private async Task<int> RingOwedAsync(int maximumItems, CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var owed in await captures.ListBellsOwedAsync(maximumItems, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // A Case archived since the bell was owed is not rung for.
                if (!owed.CaseArchived)
                {
                    await notifier.NotifyAsync(
                        StaffNotificationCause.PhotosReceived,
                        owed.CaseId,
                        SettleActor,
                        section: "files",
                        owed.Registration,
                        cancellationToken);
                }

                await captures.ClearBellAsync(owed.CaptureId, owed.Batch, cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return failures;
    }

    private async Task<(int Reached, int Failures)> ReachAsync(
        int maximumItems,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var reached = 0;
        var failures = 0;
        foreach (var reach in await captures.ListBatchesReachingCaseAsync(maximumItems, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ReachCaseAsync(reach, now, cutoff, cancellationToken))
                {
                    reached++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return (reached, failures);
    }

    /// <summary>
    /// Pages on past the captures that wait, so they never keep a ready one
    /// from its settle (F-R6c), until <paramref name="maximumItems"/> settled.
    /// </summary>
    private async Task<(int Examined, int Settled, int Failures)> WalkAsync(
        int maximumItems,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var examined = 0;
        var settled = 0;
        var failures = 0;
        PhotoCaptureSettleCursor? cursor = null;
        while (settled < maximumItems)
        {
            var page = await captures.ListAwaitingSettleAsync(now, cursor, maximumItems, cancellationToken);
            foreach (var candidate in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;
                try
                {
                    if (await SettleAsync(candidate, now, cutoff, cancellationToken))
                    {
                        settled++;
                        if (settled == maximumItems)
                        {
                            break;
                        }
                    }
                }
                catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
                {
                    failures++;
                }
            }

            if (page.Count < maximumItems)
            {
                break;
            }

            cursor = new(page[^1].Capture.CreatedAtUtc, page[^1].Capture.Id);
        }

        return (examined, settled, failures);
    }

    private Task<bool> SettleAsync(
        PhotoCaptureSettleCandidate candidate,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var capture = candidate.Capture;
        var batch = capture.CurrentBatch;
        if (!capture.IsDueToSettle(now)
            || !candidate.Items.Any(item => item.Batch == batch
                && item.StagedReceiptId is not null
                && PhotoCaptureStory.IsPhotographKind(item.Kind)))
        {
            // A Send with no photos settles nothing (R792).
            return Task.FromResult(false);
        }

        if (UploadInFlight(candidate.Items, batch, now))
        {
            // A photo is still on its way into this batch; the close would wait too.
            return Task.FromResult(false);
        }

        return capture.Destination switch
        {
            PhotoCaptureDestination.Case when candidate.CaseId is { } caseId =>
                SettleOnCaseAsync(capture, caseId, candidate.CaseArchived, candidate.CaseIsTriage, now, cutoff, cancellationToken),
            PhotoCaptureDestination.VehicleImages => SettleVehicleImagesAsync(capture, now, cutoff, cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    /// <summary>
    /// A batch on its Case: every receipt not yet on the Case is linked and
    /// every linked one whose filing has not started is re-driven; once each
    /// photo has filed or ended for good, the Case-side work runs if any photo
    /// filed, and the close, which checks the same again under its lock,
    /// commits the batch with its bell owed.
    /// </summary>
    private async Task<bool> SettleOnCaseAsync(
        PhotoCapture capture,
        Guid caseId,
        bool caseArchived,
        bool caseIsTriage,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var batch = capture.CurrentBatch;
        if (caseArchived)
        {
            // The link is dead and the photos already filed stay (R793);
            // nothing more is written to an archived Case.
            var archived = await captures.CloseBatchAsync(
                capture.Id, batch, new(now, cutoff, CaseId: null, Group: null, BellOwed: false, CaseSideNote: null), cancellationToken);
            return archived.Outcome == PhotoCaptureBatchClose.Closed;
        }

        var filings = await captures.ListBatchFilingsAsync(capture.Id, batch, caseId, cancellationToken);
        if (await FileOnCaseAsync(
                filings,
                caseId,
                PhotoCaptureIntakeRules.DeclaredDestinationDecider(capture),
                PhotoCaptureIntakeRules.DeclaredDestinationReason(capture),
                now,
                cutoff,
                cancellationToken))
        {
            filings = await captures.ListBatchFilingsAsync(capture.Id, batch, caseId, cancellationToken);
        }

        if (!PhotoCaptureFiling.Settled(filings, caseId, cutoff))
        {
            // A photo has not filed yet; the next run looks again.
            return false;
        }

        // A batch with no photo on the Case writes nothing there and spends
        // none of the capture's one images-complete.
        var filed = PhotoCaptureFiling.AnyPhotographFiled(filings, caseId, cutoff);
        if (filed)
        {
            await CaseSideAsync(capture, caseId, caseIsTriage, completeImages: true, cancellationToken);
        }

        var closed = await captures.CloseBatchAsync(
            capture.Id,
            batch,
            new(now, cutoff, caseId, Group: null, BellOwed: filed, CaseSideNote: capture.Note),
            cancellationToken);
        return closed.Outcome == PhotoCaptureBatchClose.Closed;
    }

    /// <summary>
    /// A Vehicle images batch: once every photo is evaluated it is matched
    /// again with the link's Principal and the typed date (R791). One eligible
    /// Case takes it as a Case batch; none or several close it as one group.
    /// </summary>
    private async Task<bool> SettleVehicleImagesAsync(
        PhotoCapture capture,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var batch = capture.CurrentBatch;
        var filings = await captures.ListBatchFilingsAsync(capture.Id, batch, caseId: null, cancellationToken);
        if (!PhotoCaptureFiling.Settled(filings, caseId: null, cutoff))
        {
            return false;
        }

        var registration = capture.NormalizedRegistration
            ?? throw new InvalidOperationException("A Vehicle images capture settles with the registration its sender typed.");
        var principalId = capture.StandingLinkId is { } standingLinkId
            ? (await standingLinks.GetAsync(standingLinkId, cancellationToken))?.PrincipalId
            : null;
        var decision = await routing.ExecuteAsync(registration, capture.IncidentDate, principalId, cancellationToken);
        if (decision is { Destination: PhotoCaptureDestination.Case, CaseId: { } matched })
        {
            // Recorded under the capture's lock, refused once the batch has
            // closed, so two settles never split the batch.
            var recorded = await captures.RecordCaseAsync(capture.Id, batch, matched, decision.Reason, cancellationToken);
            return recorded.Changed
                && await SettleOnCaseAsync(recorded.Capture, matched, caseArchived: false, caseIsTriage: false, now, cutoff, cancellationToken);
        }

        var closed = await captures.CloseBatchAsync(
            capture.Id,
            batch,
            new(now, cutoff, CaseId: null, new PhotoCaptureDeclaredGroup(registration, capture.IncidentDate), BellOwed: false, CaseSideNote: null),
            cancellationToken);
        if (closed.Outcome != PhotoCaptureBatchClose.Closed)
        {
            return false;
        }

        // The first member's replay takes the cheap branch: its evaluation is
        // complete, so only the group's routing runs, now with the whole
        // group present. Should it not run, the grouped-image sweep re-drives
        // the group.
        if (closed.Members.MinBy(item => item.Ordinal)?.StagedReceiptId is { } firstStaged)
        {
            await processQueuedIntake.ProcessAsync(firstStaged, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// A batch whose record merged into a Case: its own kept paperwork and
    /// video are linked to that Case, as the member of staff who chose it or,
    /// when Pegasus paired the record, as the work that follows the record's
    /// merge, which files them wherever the photos went, a Triage Case or a
    /// report-sent Case included (R803, R844); they are re-driven so they file
    /// as Other. Once each has filed or its filing has ended for good, the
    /// note and the fill run, and the reach is recorded with its bell owed
    /// (R804). The record's merge completed the images unless the capture
    /// already had (R841). An archived Case is recorded and nothing is
    /// written to it.
    /// </summary>
    private async Task<bool> ReachCaseAsync(
        PhotoCaptureBatchReach reach,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var capture = reach.Capture;
        if (!reach.CaseArchived)
        {
            var decider = reach.RecordLinkedByStaffId is { } staffId
                ? ActionActor.Staff(staffId, [StaffRole.User])
                : ActionActor.SystemWorker(PhotoCaptureIntakeRules.ReachActorId);
            var reason = reach.RecordLinkedByStaffId is not null && !string.IsNullOrWhiteSpace(reach.RecordLinkReason)
                ? reach.RecordLinkReason
                : PhotoCaptureIntakeRules.RoutedDestinationReason;
            var kept = (await captures.ListBatchFilingsAsync(capture.Id, reach.Batch, reach.CaseId, cancellationToken))
                .Where(filing => !PhotoCaptureStory.IsPhotographKind(filing.Item.Kind))
                .ToArray();
            if (await FileOnCaseAsync(kept, reach.CaseId, decider, reason, now, cutoff, cancellationToken))
            {
                kept = (await captures.ListBatchFilingsAsync(capture.Id, reach.Batch, reach.CaseId, cancellationToken))
                    .Where(filing => !PhotoCaptureStory.IsPhotographKind(filing.Item.Kind))
                    .ToArray();
            }

            if (!PhotoCaptureFiling.KeptFilesSettled(kept, reach.CaseId, cutoff))
            {
                // A kept file has no receipt yet, or has not filed yet.
                return false;
            }

            await CaseSideAsync(capture, reach.CaseId, reach.CaseIsTriage, completeImages: false, cancellationToken);
        }

        return await captures.RecordBatchReachedAsync(
            capture.Id, reach.Batch, reach.CaseId, bellOwed: !reach.CaseArchived, now, cancellationToken);
    }

    /// <summary>
    /// Links each item not yet on any Case to <paramref name="caseId"/> as the
    /// given decider's decision, keyed by receipt and Case, and re-drives
    /// each item now linked there whose filing has not started, its link's
    /// or an earlier run's: the pipeline's processing replays safely and
    /// files a linked receipt. Answers whether anything was linked or
    /// re-driven, so the caller reads the batch again.
    /// </summary>
    private async Task<bool> FileOnCaseAsync(
        IReadOnlyList<PhotoCaptureItemFiling> filings,
        Guid caseId,
        ActionActor decider,
        string reason,
        DateTimeOffset now,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var acted = false;
        foreach (var filing in filings)
        {
            if (filing.Item.IntakeReceiptId is not { } receiptId || filing.Item.StagedReceiptId is not { } stagedReceiptId)
            {
                continue;
            }

            var state = PhotoCaptureFiling.StateOf(filing, caseId, cutoff);
            if (state == PhotoCaptureFilingState.Unlinked)
            {
                var outcome = await mutations.LinkDeclaredDestinationAsync(
                    new(
                        receiptId,
                        caseId,
                        decider,
                        string.Create(CultureInfo.InvariantCulture, $"photo-capture-route:{receiptId:N}:{caseId:N}"),
                        reason),
                    now,
                    cancellationToken);
                acted = true;
                if (outcome is not (DeclaredDestinationLinkOutcome.Linked or DeclaredDestinationLinkOutcome.AlreadyLinked))
                {
                    continue;
                }
            }
            else if (state != PhotoCaptureFilingState.Linked)
            {
                continue;
            }

            await processQueuedIntake.ProcessAsync(stagedReceiptId, cancellationToken);
            acted = true;
        }

        return acted;
    }

    /// <summary>
    /// The Case-side effects of a capture's photos being on its Case, each
    /// keyed so a repeat writes nothing twice: the images once per capture
    /// (R792), the note once per wording, and the fill of empty fields only.
    /// A Triage Case keeps no Case Notes, so it takes the same words as its
    /// own Triage notes (operator, 10 November 2026; R845).
    /// </summary>
    private async Task CaseSideAsync(
        PhotoCapture capture,
        Guid caseId,
        bool caseIsTriage,
        bool completeImages,
        CancellationToken cancellationToken)
    {
        if (completeImages && capture.ImagesCompletedAtUtc is null)
        {
            await caseWriter.CompleteImagesAsync(
                caseId,
                capture.Id,
                string.Create(CultureInfo.InvariantCulture, $"photo-capture-images:{capture.Id:N}"),
                ImagesReason,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(capture.Note))
        {
            var note = CustomerNotePrefix + capture.Note;
            note = note.Length <= AddCaseNote.MaximumLength ? note : note[..AddCaseNote.MaximumLength];
            var operationKey = NoteOperationKey(capture.Id, capture.Note);
            if (caseIsTriage)
            {
                await AddTriageNotesAsync(caseId, operationKey, note, cancellationToken);
            }
            else
            {
                await addCaseNote.ExecuteAsync(
                    new AddCaseNoteRequest(caseId, SettleActor, operationKey, note),
                    cancellationToken);
            }
        }

        var values = PhotoCaptureCaseFields.Values(capture);
        if (values.Count > 0)
        {
            await caseWriter.FillEmptyFieldsAsync(
                caseId,
                values,
                capture.Id,
                PhotoCaptureCaseFields.SourceLabel(capture),
                cancellationToken);
        }
    }

    /// <summary>
    /// The note on a Triage Case (R845): a Triage note holds
    /// <see cref="TriageNotes.MaximumLength"/> characters, so a longer note
    /// is written as up to four Triage notes, in order, the words starting
    /// the first. Each part has its own key, the note's key and its place,
    /// so a repeat replays each part and a changed note is appended whole. A
    /// completed or cancelled Triage Case takes none; the whole note stays on
    /// the photo link's page.
    /// </summary>
    private async Task AddTriageNotesAsync(Guid caseId, string noteOperationKey, string note, CancellationToken cancellationToken)
    {
        var parts = note.Chunk(TriageNotes.MaximumLength).Take(MaximumTriageNotes).ToArray();
        for (var index = 0; index < parts.Length; index++)
        {
            var part = new string(parts[index]);
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            await addTriageNote.ExecuteForSystemAsync(
                caseId,
                SettleActor,
                string.Create(CultureInfo.InvariantCulture, $"{noteOperationKey}:{index + 1}"),
                part,
                cancellationToken);
        }
    }

    /// <summary>
    /// The note's key: the same wording sent again replays, and a changed
    /// note is a new key, so it is appended (R792).
    /// </summary>
    internal static string NoteOperationKey(Guid captureId, string note)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(note)));
        return string.Create(CultureInfo.InvariantCulture, $"photo-capture-note:{captureId:N}:{digest[..16]}");
    }

    private static bool UploadInFlight(IReadOnlyList<PhotoCaptureItem> items, int batch, DateTimeOffset now) =>
        items.Any(item => item.Batch == batch
            && item.StagedReceiptId is null
            && item.ReceivedAtUtc > now - UploadInFlightGrace);
}

/// <summary>
/// Where a batch's received items stand against the Case it settles on, and
/// whether the batch may close (FRD-28). A batch on a Case settles when every
/// photo in it has filed in Box (operator, 9 November 2026; R792). A photo
/// whose filing has ended for good without filing no longer holds its batch
/// open, and the batch settles on the photos that did file: its work failed
/// for good before its filing started, its association was withdrawn or moved
/// to another Case, its last filing replay was exhausted, or Pegasus made its
/// link, its filing never started and the replay window
/// (<see cref="IntakeEscapeWindow"/>) has closed, after which nothing files
/// it. A filing that has started holds the batch until it files or its replay
/// is exhausted, whatever the link's age: the pending custody is confirmed
/// with no window. This is the operator's rule for a photo that can never
/// file (operator, 10 November 2026; R840). Paperwork and video in a batch on a Case hold
/// it only until each has a receipt and is linked; a Vehicle images batch
/// waits only for its photos' evaluation. A batch whose record merged into a
/// Case records its reach once each of its kept files has filed there or
/// its filing has ended, as a photograph holds its batch
/// (<see cref="KeptFilesSettled"/>; R844).
/// </summary>
public static class PhotoCaptureFiling
{
    public static PhotoCaptureFilingState StateOf(PhotoCaptureItemFiling filing, Guid? caseId, DateTimeOffset systemFilingCutoff)
    {
        ArgumentNullException.ThrowIfNull(filing);
        if (filing.Item.IntakeReceiptId is null)
        {
            return filing.WorkFailed ? PhotoCaptureFilingState.Failed : PhotoCaptureFilingState.Pending;
        }

        // With no Case, a receipt is all a group member needs, whatever its
        // work did after the evaluation.
        if (caseId is not { } onCase)
        {
            return PhotoCaptureFilingState.Evaluated;
        }

        if (filing.Filed)
        {
            return PhotoCaptureFilingState.Filed;
        }

        var linkedHere = filing.Link is { IsActive: true } && filing.Link.CaseId == onCase;
        var exhausted = linkedHere && filing.FilingExhaustedAtUtc > filing.Link!.LinkedAtUtc;
        // A filing under way can still be confirmed by the pending-custody
        // reconciliation, which has no window, so it holds the batch until it
        // files or its replay is exhausted, whatever the work or the link's age.
        if (linkedHere && filing.FilingStarted && !exhausted)
        {
            return PhotoCaptureFilingState.Filing;
        }

        if (filing.WorkFailed)
        {
            return PhotoCaptureFilingState.Failed;
        }

        if (filing.Link is null)
        {
            return PhotoCaptureFilingState.Unlinked;
        }

        if (!linkedHere)
        {
            return PhotoCaptureFilingState.Elsewhere;
        }

        // Only a link whose filing never started ends with the replay window.
        if (exhausted
            || (!filing.FilingStarted && filing.Link.BySystem && filing.Link.LinkedAtUtc <= systemFilingCutoff))
        {
            return PhotoCaptureFilingState.Ended;
        }

        return PhotoCaptureFilingState.Linked;
    }

    /// <summary>Whether an item in this state holds its batch open.</summary>
    public static bool Holds(PhotoCaptureFilingState state, PhotoCaptureItemKind kind, bool onCase) =>
        PhotoCaptureStory.IsPhotographKind(kind)
            ? HoldsUntilFiled(state)
            : onCase && state is PhotoCaptureFilingState.Pending or PhotoCaptureFilingState.Unlinked;

    /// <summary>
    /// Whether no kept file (paperwork, video) of a batch whose record
    /// reached <paramref name="caseId"/> still holds the reach: each has filed
    /// there, or its filing has ended for good, as a photograph holds its
    /// batch (operator, 10 November 2026; R844).
    /// </summary>
    public static bool KeptFilesSettled(IEnumerable<PhotoCaptureItemFiling> filings, Guid caseId, DateTimeOffset systemFilingCutoff)
    {
        ArgumentNullException.ThrowIfNull(filings);
        return !filings.Any(filing => !PhotoCaptureStory.IsPhotographKind(filing.Item.Kind)
            && HoldsUntilFiled(StateOf(filing, caseId, systemFilingCutoff)));
    }

    /// <summary>Whether no received item holds the batch open.</summary>
    public static bool Settled(IEnumerable<PhotoCaptureItemFiling> filings, Guid? caseId, DateTimeOffset systemFilingCutoff)
    {
        ArgumentNullException.ThrowIfNull(filings);
        return !filings.Any(filing =>
            Holds(StateOf(filing, caseId, systemFilingCutoff), filing.Item.Kind, caseId is not null));
    }

    /// <summary>The states that hold until the item files or its filing ends for good.</summary>
    private static bool HoldsUntilFiled(PhotoCaptureFilingState state) =>
        state is PhotoCaptureFilingState.Pending
            or PhotoCaptureFilingState.Unlinked
            or PhotoCaptureFilingState.Linked
            or PhotoCaptureFilingState.Filing;

    /// <summary>Whether at least one photograph of the batch has filed on the Case.</summary>
    public static bool AnyPhotographFiled(IEnumerable<PhotoCaptureItemFiling> filings, Guid caseId, DateTimeOffset systemFilingCutoff)
    {
        ArgumentNullException.ThrowIfNull(filings);
        return filings.Any(filing => PhotoCaptureStory.IsPhotographKind(filing.Item.Kind)
            && StateOf(filing, caseId, systemFilingCutoff) == PhotoCaptureFilingState.Filed);
    }
}

/// <summary>
/// Which Case fields a capture's typed details may fill, and with what
/// (operator, 9 November 2026: fill empty fields with "Photo link"
/// provenance). The registration is never written: it routed the capture.
/// A view that has no Case field (roadworthy, outcome, in hire, airbags,
/// steering) stays on the capture and is shown with the photos, and so does
/// the typed inspection location: staff type the address if they want it
/// (operator, 9 November 2026; R811, narrowing R783).
/// </summary>
public static class PhotoCaptureCaseFields
{
    public const string PolicyKey = "photo-link-fill";
    public const int PolicyVersion = 1;

    public static IReadOnlyDictionary<string, string> Values(PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(capture.CustomerName))
        {
            values[CaseDataFieldNames.ClaimantName] = capture.CustomerName;
        }

        if (!string.IsNullOrWhiteSpace(capture.Mobile))
        {
            values[CaseDataFieldNames.ClaimantContactNumber] = capture.Mobile;
        }

        if (capture.IncidentDate is { } date)
        {
            values[CaseDataFieldNames.IncidentDate] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (capture.Details.Mileage is { } mileage)
        {
            values[CaseDataFieldNames.VehicleMileage] = mileage.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }

    public static string SourceLabel(PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return capture.Origin switch
        {
            PhotoCaptureOrigin.CaseLink => "Photo link",
            PhotoCaptureOrigin.Website => "Photo link (website)",
            _ => "Photo link (standing)"
        };
    }
}
