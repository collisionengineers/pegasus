using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The reading intake already recorded of a standalone Audit's original report
/// (<see cref="RecordThirdPartyReportReading"/>), as the Original report cells
/// read it. The same document is not read twice (operator, 11 October 2026):
/// acceptance seeds the Audit from this reading when one exists, and reads the
/// report only when none does.
/// </summary>
public sealed class RecordedOriginalReportReading(
    IStandaloneAuditEvidenceQueries evidence,
    IIntakeReceiptQueries receipts,
    IRetainedInstructionAnalysisStore analyses,
    IIntakeOcrOperationStore ocrOperations)
{
    /// <summary>
    /// The recorded reading of the receipt's original report, or null when none
    /// was recorded for the same report content: a scanned report is read from
    /// its later OCR recording when the first one had no text to read.
    /// </summary>
    public async Task<OriginalReportReading?> FindAsync(
        Guid receiptId,
        Guid standaloneAuditEvidenceId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FindRecordedAsync(receiptId, standaloneAuditEvidenceId, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // No recorded reading is the answer the caller already handles by
            // reading the report; a fault here never refuses the Case.
            return null;
        }
    }

    private async Task<OriginalReportReading?> FindRecordedAsync(
        Guid receiptId,
        Guid standaloneAuditEvidenceId,
        CancellationToken cancellationToken)
    {
        var recorded = await evidence.GetForReceiptAsync(receiptId, cancellationToken);
        if (recorded is null || recorded.Id != standaloneAuditEvidenceId)
        {
            return null;
        }

        var asset = await receipts.GetAssetAsync(receiptId, recorded.OriginalReportAssetId, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var analysis = await RecordThirdPartyReportReading.FindRecordedAsync(
            asset, analyses, ocrOperations, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        var rows = analysis.Candidates;
        return OriginalReportPrefillPolicy.Read(
            asset.ContentHash.ToLowerInvariant(),
            Usable(First(rows, ThirdPartyReportFields.Issuer)),
            InstructionFieldEngine.ParseDate(Usable(First(rows, ThirdPartyReportFields.ReportDate))),
            Usable(First(rows, ThirdPartyReportFields.Roadworthiness)),
            Printed(First(rows, ThirdPartyReportFields.Outcome)),
            Printed(First(rows, ThirdPartyReportFields.Repairability)));
    }

    private static RetainedInstructionCandidate? First(
        IReadOnlyList<RetainedInstructionCandidate> rows,
        string field) =>
        rows.FirstOrDefault(row => row.Field == field);

    private static string? Usable(RetainedInstructionCandidate? row) =>
        row is { Disposition: SourceCandidateDisposition.Usable } ? row.NormalizedValue : null;

    private static PrintedOutcome? Printed(RetainedInstructionCandidate? row) =>
        row is null ? null : new(Usable(row), row.Disposition);
}
