using System.Diagnostics;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// Reads one retained document as a third-party engineer report and records
/// what it says as ordinary source candidates, beside the retained asset.
///
/// It changes no receipt decision, allocates nothing and writes no Engineer
/// value — a report remains third-party evidence until the Audit's own path
/// accepts a figure from it. Intake calls it once per candidate document when
/// the source is retained; the OCR path calls it again for the same document
/// once its scanned pages have text, under a key of its own.
/// </summary>
public sealed class RecordThirdPartyReportReading(
    IRetainedInstructionAnalysisStore store,
    TimeProvider timeProvider)
{
    /// <summary>The span tag that says how a report reading ended.</summary>
    public const string OutcomeTag = "intake.third_party_report.outcome";

    /// <summary>The exception type behind a reading that was not recorded.</summary>
    public const string FailureTag = "intake.third_party_report.failure_type";

    /// <summary>The intake-time key for a document: one reading per retained asset.</summary>
    public static string OperationKey(IntakeAssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return $"{ThirdPartyReportAnalysis.PolicyKey}:{asset.Id}";
    }

    /// <summary>The key for the same document read from a completed OCR operation.</summary>
    public static string OcrOperationKey(IntakeAssetRecord asset, Guid ocrOperationId)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return $"{ThirdPartyReportAnalysis.PolicyKey}:{asset.Id}:ocr:{ocrOperationId:N}";
    }

    /// <summary>
    /// Reads <paramref name="document"/> — one document's fragments, scanned
    /// pages and images, nothing from the rest of its source — and records the
    /// reading under <paramref name="operationKey"/>. A document that carries
    /// no report signature and raises no finding is left alone entirely.
    /// </summary>
    public async Task ExecuteAsync(
        IntakeReceipt receipt,
        IntakeAssetRecord asset,
        IntakeSourceReadResult document,
        string operationKey,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        var extraction = ThirdPartyReportExtraction.Extract(
            document,
            new(
                receipt.Id,
                asset.ContentHash,
                Occurrence: 0,
                IntakeAssetId: asset.Id,
                ReaderVersion: document.ReaderVersion,
                // The retained file's own name, carried as the document-level
                // locator for a row with no page to point at (a scan-only
                // source names no page because its text could not be read).
                // It locates; it is never read as content, so no issuer, family
                // or field value is taken from it.
                SourceLabel: asset.FileName));
        if (!ThirdPartyReportAnalysis.IsRecordable(extraction))
        {
            // The document was read, carries no report signature and states
            // nothing about itself that a person has to act on. Saying so is
            // what makes the silence on the other paths meaningful.
            activity?.SetTag(OutcomeTag, "no_report_signature");
            return;
        }

        try
        {
            var (_, isReplay) = await store.RecordAsync(
                new(
                    Guid.NewGuid(),
                    receipt.Id,
                    asset.Id,
                    asset.ContentHash,
                    // Derived from the asset, so re-processing the same
                    // retained bytes leaves the recorded reading standing
                    // instead of writing a second set of candidates for one
                    // document. The conflict below — not the replay — is that
                    // outcome's ordinary path: a re-evaluation always moves the
                    // receipt version, so a second pass over one asset can
                    // never satisfy the store's replay check and "replayed" is
                    // reachable only where the same version is re-recorded.
                    operationKey,
                    ThirdPartyReportAnalysis.Outcome(extraction.Selection),
                    receipt.Version,
                    timeProvider.GetUtcNow(),
                    ThirdPartyReportAnalysis.ToCandidates(
                        extraction,
                        document.ReaderKey,
                        document.ReaderVersion)),
                cancellationToken);
            activity?.SetTag(OutcomeTag, isReplay ? "replayed" : "recorded");
        }
        catch (RetainedInstructionAnalysisConflictException)
        {
            // The key was already used, and the store raises one exception for
            // two different facts (IRetainedInstructionAnalysisStore.RecordAsync):
            // this document was already read at another receipt version — the
            // recorded reading stands, the bytes have not changed, and
            // overwriting is exactly what the key exists to prevent — or the key
            // is bound to another receipt or asset, where nothing was recorded
            // for this receipt at all. One tag for both would state something
            // false in the second case, so the stored row decides which is said.
            // Named on the span either way, so a conflict is distinguishable
            // from a reading that was never attempted.
            activity?.SetTag(
                OutcomeTag,
                await ConflictOutcomeAsync(operationKey, receipt.Id, asset.Id, cancellationToken));
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Source evidence is supplementary. A receipt that has already been
            // stored must not fail because a report reading could not be
            // written beside it.
            //
            // The failure is named on the span rather than swallowed. The
            // intake itself succeeded, so the span's own status stays as the
            // receipt left it: this is a named failure inside work that
            // otherwise did what it was asked.
            activity?.SetTag(OutcomeTag, "not_recorded");
            activity?.SetTag(FailureTag, exception.GetType().Name);
        }
    }

    /// <summary>
    /// Which conflict the analysis store raised, read back from the stored row
    /// rather than assumed. A probe that itself fails claims neither: an
    /// unverified conflict is its own outcome, because the point of the tag is
    /// that no path stays silent and none overstates what it knows.
    /// </summary>
    private async Task<string> ConflictOutcomeAsync(
        string operationKey,
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var stored = await store.FindByOperationKeyAsync(operationKey, cancellationToken);
            return stored is not null
                && stored.ReceiptId == receiptId
                && stored.IntakeAssetId == assetId
                    ? "recorded_reading_stands"
                    : "analysis_key_bound_elsewhere";
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return "recorded_reading_unverified";
        }
    }
}
