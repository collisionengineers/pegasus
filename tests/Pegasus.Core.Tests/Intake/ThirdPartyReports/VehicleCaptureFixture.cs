using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A receipt whose PDF intake recorded as a capture document, and the
/// doubles the capture reading and fill run against.
/// </summary>
internal static class VehicleCaptureFixture
{
    public static IntakeReceipt Receipt(params IntakeAssetRecord[] assets) =>
        new(
            Guid.NewGuid(),
            "LINE_LEVEL_ESTIMATE.pdf",
            "application/pdf",
            200_000,
            new string('a', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "capture-token"),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            IntakeDecision.NeedsSorting,
            "No Principal route.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            assets.Length == 0 ? [Pdf()] : assets);

    public static IntakeAssetRecord Pdf(string fileName = "LINE_LEVEL_ESTIMATE.pdf") =>
        new(
            Guid.NewGuid(),
            "mail.eml, attachment 1: " + fileName,
            fileName,
            "application/pdf",
            IntakeAssetKind.Attachment,
            IntakeAssetDisposition.Attachment,
            200_000,
            Convert.ToHexString(Guid.NewGuid().ToByteArray()) + new string('b', 32),
            "storage/" + fileName,
            null,
            null,
            null,
            null);

    /// <summary>One usable capture row per field value given.</summary>
    public static IReadOnlyList<RetainedInstructionCandidate> Rows(params (string Field, string Value)[] values) =>
        [
            .. values.Select(value => new RetainedInstructionCandidate(
                Guid.NewGuid(),
                ThirdPartyReportProfiles.VehicleCaptureDocumentRole,
                value.Field,
                null,
                null,
                value.Value,
                value.Value,
                null,
                null,
                "LINE_LEVEL_ESTIMATE.pdf",
                1,
                0,
                "intake_source_reader",
                "1",
                ThirdPartyReportAnalysis.PolicyKey,
                ThirdPartyReportExtraction.ProfileVersion,
                SourceCandidateDisposition.Usable))
        ];

    /// <summary>The readings intake recorded, by operation key.</summary>
    public sealed class Analyses : IRetainedInstructionAnalysisStore
    {
        private readonly Dictionary<string, RetainedInstructionAnalysis> byKey = new(StringComparer.Ordinal);

        public bool Fails { get; set; }

        public Analyses Record(IntakeReceipt receipt, IntakeAssetRecord asset, IReadOnlyList<RetainedInstructionCandidate> rows)
        {
            var key = RecordThirdPartyReportReading.OperationKey(asset);
            byKey[key] = new(
                Guid.NewGuid(),
                receipt.Id,
                asset.Id,
                asset.ContentHash,
                key,
                RetainedInstructionAnalysisOutcome.NoProfile,
                0,
                DateTimeOffset.UnixEpoch,
                rows);
            return this;
        }

        public Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
            RetainedInstructionAnalysis analysis,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
            string operationKey,
            CancellationToken cancellationToken = default) =>
            Fails
                ? throw new InvalidOperationException("The analysis store is unavailable.")
                : Task.FromResult(byKey.GetValueOrDefault(operationKey));
    }

    public sealed class Receipts(params IntakeReceipt[] receipts) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(receipts.SingleOrDefault(receipt => receipt.Id == id));
    }

    public sealed class FillSpy : IFillCaseFromVehicleCapture
    {
        public List<(Guid CaseId, VehicleCaptureReading Reading)> Calls { get; } = [];

        public Task FillAsync(Guid caseId, VehicleCaptureReading reading, CancellationToken cancellationToken)
        {
            Calls.Add((caseId, reading));
            return Task.CompletedTask;
        }
    }
}
