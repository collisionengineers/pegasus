using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;

namespace Pegasus.Core.PhotoCapture;

public sealed record SettlePhotoCapturesResult(int Examined, int Settled, int ReachedCase, int Failures);

/// <summary>
/// The Worker's settle (FRD-28): once the sender has pressed Send, or the
/// link has run out with photos on it, and every photo of the batch has been
/// processed, the capture is settled. A Case-destination capture completes
/// the Case's images, writes the sender's note into the Case's Notes, fills
/// the Case's empty fields from the sender's details with "Photo link"
/// provenance and tells the Case's Engineer (operator, 9 November 2026). A
/// Vehicle-images-destination capture forms its submission group from its
/// receipts and re-drives the first member through the ordinary pipeline,
/// whose group routing registers the Vehicle images record, or files on a
/// Case that has appeared since (FRD-19). A settled capture that later
/// reaches a Case finishes the Case-side work then. Each capture is settled
/// once per batch; a retried settle replays by its operation keys.
/// </summary>
public sealed class SettlePhotoCaptures(
    IPhotoCaptureStore captures,
    IIntakeSubmissionGroupStore groups,
    IProcessQueuedIntake processQueuedIntake,
    IPhotoCaptureCaseWriter caseWriter,
    IAddCaseNote addCaseNote,
    ICaseStaffNotifier notifier,
    TimeProvider timeProvider)
{
    private static readonly ActionActor SettleActor = ActionActor.SystemWorker(PhotoCaptureIntakeRules.SettleActorId);

    public async Task<SettlePhotoCapturesResult> ExecuteAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var examined = 0;
        var settled = 0;
        var reached = 0;
        var failures = 0;

        foreach (var pending in await captures.ListAwaitingSettleAsync(now, maximumItems, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            examined++;
            try
            {
                if (await SettleAsync(pending, now, cancellationToken))
                {
                    settled++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        foreach (var (pending, caseId, caseReference) in await captures.ListSettledReachingCaseAsync(maximumItems, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var capture = await captures.RecordCaseAsync(pending.Capture.Id, caseId, caseReference, cancellationToken);
                await FinishCaseSideAsync(capture, pending.Items.Count > 0, cancellationToken);
                reached++;
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(examined, settled, reached, failures);
    }

    private async Task<bool> SettleAsync(PhotoCaptureWithItems pending, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var capture = pending.Capture;
        var batch = capture.CurrentBatch;
        // An item whose upload never completed (no staged receipt) is not a photo the capture holds.
        var items = pending.Items
            .Where(item => item.Batch == batch && item.StagedReceiptId is not null)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        if (items.Length > 0 && !await captures.AllItemsProcessedAsync(capture.Id, batch, cancellationToken))
        {
            // A photo is still being processed; the next tick settles.
            return false;
        }

        Guid? groupId = null;
        if (capture.Destination == PhotoCaptureDestination.VehicleImages && items.Length > 0)
        {
            groupId = await FormGroupAsync(capture, items, cancellationToken);
        }

        var updated = await captures.RecordSettledAsync(capture.Id, capture.Version, groupId, now, cancellationToken);
        if (updated.Destination == PhotoCaptureDestination.Case && updated.CaseId is not null)
        {
            await FinishCaseSideAsync(updated, pending.Items.Count > 0, cancellationToken);
        }

        if (groupId is { } formed && items[0].StagedReceiptId is { } firstStaged)
        {
            // The replay takes the cheap branch: the evaluation is complete,
            // so only the automation runs, now with the whole group present.
            await processQueuedIntake.ProcessAsync(firstStaged, cancellationToken);
        }

        return true;
    }

    private async Task<Guid> FormGroupAsync(
        PhotoCapture capture,
        PhotoCaptureItem[] items,
        CancellationToken cancellationToken)
    {
        var groupId = PhotoCaptureIntakeRules.BatchGroupId(capture.Id, capture.CurrentBatch);
        var group = await groups.GetOrCreateDeclaredAsync(
            groupId,
            IntakeSourceChannel.PhotoCapture,
            PhotoCaptureIntakeRules.BatchToken(capture.Id, capture.CurrentBatch),
            items.Length,
            IntakeActorIdentity.PhotoCapture(capture.Id),
            items[0].ReceivedAtUtc,
            capture.NormalizedRegistration
                ?? throw new InvalidOperationException("A Vehicle images capture settles with the registration its sender typed."),
            cancellationToken);
        foreach (var item in items)
        {
            if (item.StagedReceiptId is { } stagedReceiptId)
            {
                await groups.AddMemberAsync(group.Id, item.Ordinal, new ReceivedIntake(stagedReceiptId, false), cancellationToken);
            }
        }

        return group.Id;
    }

    /// <summary>
    /// The Case-side effects of a capture's photos being on a Case: once per
    /// capture and batch, keyed so a retried sweep repeats none of them.
    /// </summary>
    private async Task FinishCaseSideAsync(
        PhotoCapture capture,
        bool hasItems,
        CancellationToken cancellationToken)
    {
        if (capture.CaseId is not { } caseId)
        {
            return;
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"photo-capture-settle:{capture.Id:N}:{capture.SettledBatches}");
        await caseWriter.CompleteImagesAsync(
            caseId,
            key + ":images",
            "The photo link's photos are in: the sender pressed Send, or the link ran out with photos on it.",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(capture.Note))
        {
            var note = "Customer's note (photo link): " + capture.Note;
            await addCaseNote.ExecuteAsync(
                new AddCaseNoteRequest(
                    caseId,
                    SettleActor,
                    key + ":note",
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

        if (hasItems)
        {
            await notifier.NotifyAsync(
                StaffNotificationCause.PhotosReceived,
                caseId,
                SettleActor,
                section: "files",
                capture.NormalizedRegistration,
                cancellationToken);
        }
    }
}

/// <summary>
/// Which Case fields a capture's typed details may fill, and with what
/// (operator, 9 November 2026: fill empty fields with "Photo link"
/// provenance). The registration is never written: it routed the capture.
/// A view that has no Case field (roadworthy, outcome, in hire, airbags,
/// steering) stays on the capture and is shown with the photos.
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

        var location = capture.Details.InspectionLocation;
        if (!string.IsNullOrWhiteSpace(location))
        {
            values[CaseDataFieldNames.InspectionAddress] = location;
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
