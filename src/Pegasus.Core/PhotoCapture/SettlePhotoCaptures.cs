using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;

namespace Pegasus.Core.PhotoCapture;

public sealed record SettlePhotoCapturesResult(int Examined, int Settled, int ReachedCase, int Failures);

/// <summary>
/// The Worker's settle (FRD-28). A capture's open batch settles once it holds
/// a photo and the sender has pressed Send, or the link has expired or was
/// cancelled (operator, 9 November 2026; R792, R794).
/// <para>
/// A batch on a Case waits until every photo has filed on the Case, then the
/// Case-side work runs: the Case's images complete once per capture, the
/// sender's note is written once (a changed note is appended), the empty
/// fields fill, and only then does the batch close and the Engineer's bell
/// ring, once for the batch. An archived Case closes the batch with nothing
/// written to it (R793). A Vehicle images batch waits until every photo is
/// evaluated and is then matched again with the link's Principal and the
/// typed accident date (R791): one eligible Case takes the batch straight
/// onto that Case, with no group and no record; none or several close the
/// batch as one submission group of its photographs, carrying the typed
/// registration and date, whose first member is re-driven to register the
/// record. A capture whose record later reaches a Case finishes its Case-side
/// work then, files its kept paperwork and video as Other (R803), records the
/// Case and rings the bell once (R804).
/// </para>
/// <para>
/// Every step is safe to repeat: the batch closes under the capture's lock
/// and says when it already had; the images, the note, the fill and each
/// link are keyed; and a capture that has to wait costs the next one nothing.
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
    ICaseStaffNotifier notifier,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How long an item with no receipt yet holds its batch open: an upload
    /// still on its way. An older one never reached intake, and its retry
    /// joins the next batch.
    /// </summary>
    public static readonly TimeSpan UploadInFlightGrace = TimeSpan.FromMinutes(5);

    private const string ImagesReason =
        "The photo link's photos are in: the sender pressed Send, or the link ran out with photos on it.";

    private static readonly ActionActor SettleActor = ActionActor.SystemWorker(PhotoCaptureIntakeRules.SettleActorId);

    public async Task<SettlePhotoCapturesResult> ExecuteAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var examined = 0;
        var settled = 0;
        var reached = 0;
        var failures = 0;

        // Pages on past the captures that wait, so they never keep a ready
        // one from its settle (F-R6c).
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
                    if (await SettleAsync(candidate, now, cancellationToken))
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

        foreach (var candidate in await captures.ListSettledReachingCaseAsync(maximumItems, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ReachCaseAsync(candidate, now, cancellationToken))
                {
                    reached++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(examined, settled, reached, failures);
    }

    private Task<bool> SettleAsync(PhotoCaptureSettleCandidate candidate, DateTimeOffset now, CancellationToken cancellationToken)
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
                SettleOnCaseAsync(candidate, caseId, now, cancellationToken),
            PhotoCaptureDestination.VehicleImages => SettleVehicleImagesAsync(candidate, now, cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    /// <summary>
    /// A batch on its Case: every receipt that waits is filed, and once each
    /// photo has filed the Case-side work runs, the batch closes and the bell
    /// rings for it.
    /// </summary>
    private async Task<bool> SettleOnCaseAsync(
        PhotoCaptureSettleCandidate candidate,
        Guid caseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var capture = candidate.Capture;
        var batch = capture.CurrentBatch;
        if (candidate.CaseArchived)
        {
            // The link is dead and the photos already filed stay (R793);
            // nothing more is written to an archived Case.
            var archived = await captures.CloseBatchAsync(capture.Id, batch, now, group: null, cancellationToken);
            return archived.Outcome == PhotoCaptureBatchClose.Closed;
        }

        // A receipt that arrived before the capture had this Case (one the
        // settle routed) files now, as the routing decided.
        foreach (var item in candidate.Items.Where(item => item.Batch == batch && item.DeclaredCaseId != caseId))
        {
            await FileOnCaseAsync(capture, item, caseId, now, cancellationToken);
        }

        if (!await captures.AllItemsSettledAsync(capture.Id, batch, caseId, cancellationToken))
        {
            // A photo has not filed yet; the next tick looks again.
            return false;
        }

        await CaseSideAsync(candidate, capture, caseId, completeImages: true, cancellationToken);
        var closed = await captures.CloseBatchAsync(capture.Id, batch, now, group: null, cancellationToken);
        if (closed.Outcome != PhotoCaptureBatchClose.Closed)
        {
            return false;
        }

        await RingAsync(capture, caseId, cancellationToken);
        return true;
    }

    /// <summary>
    /// A Vehicle images batch: once every photo is evaluated it is matched
    /// again with the link's Principal and the typed date (R791). One eligible
    /// Case takes it as a Case batch; none or several close it as one group.
    /// </summary>
    private async Task<bool> SettleVehicleImagesAsync(
        PhotoCaptureSettleCandidate candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var capture = candidate.Capture;
        var batch = capture.CurrentBatch;
        if (!await captures.AllItemsSettledAsync(capture.Id, batch, caseId: null, cancellationToken))
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
            var recorded = await captures.RecordCaseAsync(
                capture.Id, matched, decision.Reason, imagesCompletedAtUtc: null, cancellationToken);
            // A capture that had its Case already is the next listing's Case batch.
            return recorded.Changed
                && await SettleOnCaseAsync(
                    candidate with { Capture = recorded.Capture, CaseId = matched, CaseArchived = false, CaseIsTriage = false },
                    matched,
                    now,
                    cancellationToken);
        }

        var closed = await captures.CloseBatchAsync(
            capture.Id, batch, now, new PhotoCaptureDeclaredGroup(registration, capture.IncidentDate), cancellationToken);
        if (closed.Outcome != PhotoCaptureBatchClose.Closed)
        {
            return false;
        }

        // The first member's replay takes the cheap branch: its evaluation is
        // complete, so only the group's routing runs, now with the whole
        // group present. Should it not run, the grouped-image sweep re-drives
        // the group.
        var first = closed.Items
            .Where(item => PhotoCaptureStory.IsPhotographKind(item.Kind) && item.StagedReceiptId is not null)
            .MinBy(item => item.Ordinal);
        if (first?.StagedReceiptId is { } firstStaged)
        {
            await processQueuedIntake.ProcessAsync(firstStaged, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// A settled capture whose Vehicle images record reached a Case: the
    /// note and the fill, its kept paperwork and video filed as Other
    /// (R803), then the Case recorded on the capture with its images marker,
    /// the fold having completed the images; the bell rings once, when the
    /// Case is recorded (R804). An archived Case is recorded and nothing is
    /// written to it.
    /// </summary>
    private async Task<bool> ReachCaseAsync(
        PhotoCaptureSettleCandidate candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (candidate.CaseId is not { } caseId)
        {
            return false;
        }

        var capture = candidate.Capture;
        if (!candidate.CaseArchived)
        {
            await CaseSideAsync(candidate, capture, caseId, completeImages: false, cancellationToken);
            foreach (var item in candidate.Items.Where(item =>
                         !PhotoCaptureStory.IsPhotographKind(item.Kind) && item.DeclaredCaseId is null))
            {
                await FileOnCaseAsync(capture, item, caseId, now, cancellationToken);
            }
        }

        var recorded = await captures.RecordCaseAsync(
            capture.Id, caseId, routingReason: null, imagesCompletedAtUtc: now, cancellationToken);
        if (!recorded.Changed)
        {
            return false;
        }

        if (!candidate.CaseArchived)
        {
            await RingAsync(capture, caseId, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Links one received item to the Case as its capture's declared
    /// destination, decided as the capture's own items are
    /// (<see cref="PhotoCaptureIntakeRules.DeclaredDestinationDecider"/>), and
    /// re-drives it so it files. Keyed by receipt and Case: a repeat changes
    /// nothing, and a receipt someone else linked first keeps that link.
    /// </summary>
    private async Task FileOnCaseAsync(
        PhotoCapture capture,
        PhotoCaptureItem item,
        Guid caseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (item.IntakeReceiptId is not { } receiptId || item.StagedReceiptId is not { } stagedReceiptId)
        {
            // Not evaluated yet: the batch waits for it, and a later tick links it.
            return;
        }

        var outcome = await mutations.LinkDeclaredDestinationAsync(
            new(
                receiptId,
                caseId,
                PhotoCaptureIntakeRules.DeclaredDestinationDecider(capture),
                string.Create(CultureInfo.InvariantCulture, $"photo-capture-route:{receiptId:N}:{caseId:N}"),
                PhotoCaptureIntakeRules.DeclaredDestinationReason(capture)),
            now,
            cancellationToken);
        if (outcome == DeclaredDestinationLinkOutcome.Linked)
        {
            // The completed receipt's replay files its associated evidence.
            await processQueuedIntake.ProcessAsync(stagedReceiptId, cancellationToken);
        }
    }

    /// <summary>
    /// The Case-side effects of a capture's photos being on its Case, each
    /// keyed so a repeat writes nothing twice: the images once per capture
    /// (R792), the note once per wording, and the fill of empty fields only.
    /// A Triage Case keeps no Notes, so it takes no note.
    /// </summary>
    private async Task CaseSideAsync(
        PhotoCaptureSettleCandidate candidate,
        PhotoCapture capture,
        Guid caseId,
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

        if (!candidate.CaseIsTriage && !string.IsNullOrWhiteSpace(capture.Note))
        {
            var note = "Customer's note (photo link): " + capture.Note;
            await addCaseNote.ExecuteAsync(
                new AddCaseNoteRequest(
                    caseId,
                    SettleActor,
                    NoteOperationKey(capture.Id, capture.Note),
                    note.Length <= AddCaseNote.MaximumLength ? note : note[..AddCaseNote.MaximumLength]),
                cancellationToken);
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

    private Task RingAsync(PhotoCapture capture, Guid caseId, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            StaffNotificationCause.PhotosReceived,
            caseId,
            SettleActor,
            section: "files",
            capture.NormalizedRegistration,
            cancellationToken);

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
