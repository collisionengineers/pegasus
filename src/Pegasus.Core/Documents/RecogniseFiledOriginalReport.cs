using System.Diagnostics;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// Recognises the original report among the files an intake receipt filed on
/// an open Audit whose original report is missing (FRD-16): the uploaded
/// file, an e-mail's attachments, whichever route filed them. Exactly one file
/// matching a catalogued report signature is recorded as the original report
/// and fills the Original report cells, as a staff Mark does. None, two or
/// more, or a file that could not be read leave the requirement for staff to
/// mark (operator, 28 September 2026). While the Case is being edited,
/// recording waits for the editor and retries (R728): the recording is
/// deferred (<see cref="IOriginalReportRecognitionDeferrals"/>) and
/// <see cref="RetryDeferredOriginalReportRecognition"/> tries it again, so the
/// filing that found the report neither fails nor waits (#1981).
/// </summary>
public sealed class RecogniseFiledOriginalReport(
    IRecogniseOriginalReportStore store,
    IReadOriginalReport originalReport,
    IOriginalReportRecognitionDeferrals? deferrals = null)
{
    private static readonly ActionActor SystemWorkerActor =
        ActionActor.SystemWorker("intake-processing");

    public async Task<OriginalReportRecognitionResult> ExecuteAsync(
        Guid caseId,
        IntakeReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var assets = receipt.AssetRecords
            .Where(OriginalReportPolicy.IsRecognitionCandidate)
            .ToDictionary(asset => asset.Id);
        if (assets.Count == 0)
        {
            return Result(OriginalReportRecognitionResult.NotApplicable);
        }

        // Nothing is read unless the Case awaits its report and the receipt's
        // files are on it. The files are found by the hash of their bytes,
        // whichever route filed them: acceptance, a matched follow-up, the fold.
        var found = await store.FindAwaitingCandidatesAsync(
            caseId,
            receipt.Id,
            [.. assets.Values.Select(asset => new FiledOriginalReportLookup(asset.Id, asset.ContentHash))],
            cancellationToken);
        if (!found.CaseAwaitsReport)
        {
            return Result(OriginalReportRecognitionResult.NotApplicable);
        }

        if (found.Filed.Count == 0)
        {
            return Result(OriginalReportRecognitionResult.AwaitingFiling);
        }

        var filed = found.Filed;

        var recognised = new List<(FiledOriginalReportCandidate Filed, OriginalReportReading Reading)>();
        foreach (var candidate in filed)
        {
            if (!assets.TryGetValue(candidate.IntakeAssetId, out var asset))
            {
                continue;
            }

            var recognition = await originalReport.RecogniseFiledAssetAsync(receipt.Id, asset, cancellationToken);
            if (recognition.Outcome == OriginalReportRecognitionOutcome.Unavailable)
            {
                // A file that could not be read may be a second report.
                return Result(OriginalReportRecognitionResult.Unreadable);
            }

            if (recognition is { Outcome: OriginalReportRecognitionOutcome.Recognised, Reading: { } reading })
            {
                recognised.Add((candidate, reading));
                if (recognised.Count == 2)
                {
                    // Two are already several; the rest need not be read.
                    break;
                }
            }
        }

        if (recognised.Count != 1)
        {
            return Result(recognised.Count == 0
                ? OriginalReportRecognitionResult.NoneRecognised
                : OriginalReportRecognitionResult.SeveralRecognised);
        }

        var (report, reportReading) = recognised[0];
        OriginalReportRecorded? recorded;
        try
        {
            recorded = await store.RecordRecognisedAsync(
                new(
                    caseId,
                    receipt.Id,
                    report.DocumentOccurrenceId,
                    report.DocumentVersionId,
                    SystemWorkerActor,
                    OriginalReportRecognitionOperationKey.For(receipt.Id)),
                reportReading,
                cancellationToken);
        }
        catch (IntakeDependencyUnavailableException) when (deferrals is not null)
        {
            // An editor holds the Case, or it changed under the write.
            await deferrals.RecordAsync(caseId, receipt.Id, receipt.Version, cancellationToken);
            return Result(OriginalReportRecognitionResult.WaitingForEditor);
        }

        return Result(recorded is null
            ? OriginalReportRecognitionResult.NotApplicable
            : OriginalReportRecognitionResult.Recorded);
    }

    private static OriginalReportRecognitionResult Result(OriginalReportRecognitionResult result)
    {
        Activity.Current?.SetTag("intake.original_report_recognition", result.ToString());
        return result;
    }
}

public enum OriginalReportRecognitionResult
{
    NotApplicable,
    NoneRecognised,
    SeveralRecognised,
    Unreadable,

    /// <summary>The Case awaits its report, but none of the receipt's documents is filed on it yet.</summary>
    AwaitingFiling,
    Recorded,

    /// <summary>One report was recognised while an editor held the Case; recording it is deferred and retried (R728).</summary>
    WaitingForEditor
}

/// <summary>A receipt whose recognised original report waits to be recorded on its Case.</summary>
public sealed record OriginalReportRecognitionDeferral(Guid CaseId, Guid ReceiptId);

/// <summary>
/// The recordings of a recognised original report that waited for a Case
/// editor, kept until a later attempt decides them.
/// </summary>
public interface IOriginalReportRecognitionDeferrals
{
    /// <summary>Records that the receipt's report waits for the editor; one already waiting counts as recorded.</summary>
    Task RecordAsync(Guid caseId, Guid receiptId, long intakeVersion, CancellationToken cancellationToken);

    /// <summary>The waiting recordings, oldest first, at most <paramref name="maximumItems"/>.</summary>
    Task<IReadOnlyList<OriginalReportRecognitionDeferral>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>Ends the wait: a later attempt recorded the report or found it no longer awaited.</summary>
    Task SettleAsync(OriginalReportRecognitionDeferral deferral, CancellationToken cancellationToken);
}

public sealed record RetryDeferredOriginalReportRecognitionResult(int Candidates, int Settled, int Waiting, int Failures);

/// <summary>
/// Tries again each original report recording that waited for a Case editor
/// (R728, #1981), on the Worker's one-minute timer. A wait ends when the report
/// is recorded, or when the Case no longer awaits it, or when the receipt is
/// gone. An editor still holding the Case leaves it waiting. A fault that
/// retrying cannot clear ends it too, and the report is left for staff to mark,
/// as an unreadable file is.
/// </summary>
public sealed class RetryDeferredOriginalReportRecognition(
    IOriginalReportRecognitionDeferrals deferrals,
    IIntakeReceiptQueries receiptQueries,
    RecogniseFiledOriginalReport? recogniser = null)
{
    public async Task<RetryDeferredOriginalReportRecognitionResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        if (recogniser is null)
        {
            return new(0, 0, 0, 0);
        }

        var due = await deferrals.ListAsync(maximumItems, cancellationToken);
        var settled = 0;
        var waiting = 0;
        var failures = 0;
        foreach (var deferral in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var receipt = await receiptQueries.GetAsync(deferral.ReceiptId, cancellationToken);
                var result = receipt is null
                    ? OriginalReportRecognitionResult.NotApplicable
                    : await recogniser.ExecuteAsync(deferral.CaseId, receipt, cancellationToken);
                if (result == OriginalReportRecognitionResult.WaitingForEditor)
                {
                    waiting++;
                    continue;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsTransientFailure(exception))
            {
                failures++;
                continue;
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }

            await deferrals.SettleAsync(deferral, cancellationToken);
            settled++;
        }

        return new(due.Count, settled, waiting, failures);
    }
}

public static class OriginalReportRecognitionOperationKey
{
    public static string For(Guid receiptId) => $"original-report-recognition:{receiptId:N}";
}
