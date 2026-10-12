using Pegasus.Core.Documents;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// The Cases a manually uploaded third-party report points at (operator,
/// 12 October 2026): the registration and claim reference its recorded
/// reading prints, matched in the case-match index. They are offered as
/// suggestions only; staff still confirm, and nothing links on its own.
/// </summary>
public sealed class ThirdPartyReportCaseSuggestions(
    IRetainedInstructionAnalysisStore analyses,
    IIntakeOcrOperationStore ocrOperations,
    EvaluateIntakeCaseMatch caseMatch)
{
    public async Task<IReadOnlyList<Guid>> FindAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.SourceIdentity.Channel != IntakeSourceChannel.ManualUpload)
        {
            return [];
        }

        var caseIds = new List<Guid>();
        // The same files intake reads as reports: the uploaded file itself or
        // a message's attachments, never an image and never the message.
        foreach (var asset in receipt.AssetRecords.Where(OriginalReportPolicy.IsRecognitionCandidate))
        {
            var reading = await RecordThirdPartyReportReading.FindRecordedAsync(
                asset, analyses, ocrOperations, cancellationToken);
            if (reading is null)
            {
                continue;
            }

            var registration = Usable(reading, ThirdPartyReportFields.Registration);
            var claimReference = Usable(reading, ThirdPartyReportFields.ClaimReference);
            if (registration is null && claimReference is null)
            {
                continue;
            }

            caseIds.AddRange(await caseMatch.FindSurvivingCasesAsync(
                new(claimReference, registration, null, null),
                cancellationToken));
        }

        return [.. caseIds.Distinct()];
    }

    private static string? Usable(RetainedInstructionAnalysis reading, string field) =>
        reading.Candidates.FirstOrDefault(row => row.Field == field) is
        {
            Disposition: SourceCandidateDisposition.Usable
        } row
            ? row.NormalizedValue
            : null;
}
