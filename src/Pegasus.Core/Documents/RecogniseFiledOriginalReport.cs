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
/// mark (operator, 28 September 2026).
/// </summary>
public sealed class RecogniseFiledOriginalReport(
    IRecogniseOriginalReportStore store,
    IReadOriginalReport originalReport)
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
            }
        }

        if (recognised.Count != 1)
        {
            return Result(recognised.Count == 0
                ? OriginalReportRecognitionResult.NoneRecognised
                : OriginalReportRecognitionResult.SeveralRecognised);
        }

        var (report, reportReading) = recognised[0];
        var recorded = await store.RecordRecognisedAsync(
            new(
                caseId,
                receipt.Id,
                report.DocumentOccurrenceId,
                report.DocumentVersionId,
                SystemWorkerActor,
                OriginalReportRecognitionOperationKey.For(receipt.Id)),
            reportReading,
            cancellationToken);
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
    Recorded
}

public static class OriginalReportRecognitionOperationKey
{
    public static string For(Guid receiptId) => $"original-report-recognition:{receiptId:N}";
}
