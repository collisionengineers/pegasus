using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// Recognising the original report among the files a receipt filed on an
/// Audit awaiting it (#901): exactly one recognised file is recorded; none,
/// two or more, or a file that could not be read leave it for staff to mark.
/// </summary>
public sealed class RecogniseFiledOriginalReportTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    [Fact]
    public async Task TheOneRecognisedFileIsRecordedAsTheOriginalReportByTheSystem()
    {
        var report = Pdf("report.pdf");
        var letter = Pdf("covering-letter.pdf");
        var harness = new Harness(Receipt(Email(), report, letter));
        harness.Reader.Recognise(report);

        var result = await harness.Sut.ExecuteAsync(CaseId, harness.Receipt);

        Assert.Equal(OriginalReportRecognitionResult.Recorded, result);
        var (command, reading) = Assert.Single(harness.Store.Recorded);
        Assert.Equal(harness.Store.Filed[report.Id].DocumentOccurrenceId, command.DocumentOccurrenceId);
        Assert.Equal(harness.Store.Filed[report.Id].DocumentVersionId, command.DocumentVersionId);
        Assert.Equal(CaseId, command.CaseId);
        Assert.Equal(harness.Receipt.Id, command.IntakeReceiptId);
        Assert.Equal(ActorKind.SystemWorker, command.Actor.Kind);
        Assert.Equal(OriginalReportRecognitionOperationKey.For(harness.Receipt.Id), command.OperationKey);
        Assert.Equal(harness.Reader.Readings[report.Id], reading);
    }

    [Fact]
    public async Task NoRecognisedFileRecordsNothing()
    {
        var harness = new Harness(Receipt(Pdf("report.pdf")));

        Assert.Equal(
            OriginalReportRecognitionResult.NoneRecognised,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Empty(harness.Store.Recorded);
    }

    [Fact]
    public async Task TwoRecognisedFilesRecordNeitherAndStaffChoose()
    {
        var report = Pdf("report.pdf");
        var supplementary = Pdf("supplementary.pdf");
        var harness = new Harness(Receipt(Email(), report, supplementary));
        harness.Reader.Recognise(report);
        harness.Reader.Recognise(supplementary);

        Assert.Equal(
            OriginalReportRecognitionResult.SeveralRecognised,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Empty(harness.Store.Recorded);
    }

    [Fact]
    public async Task AFileThatCouldNotBeReadRecordsNothingBecauseItMayBeASecondReport()
    {
        var report = Pdf("report.pdf");
        var unread = Pdf("unread.pdf");
        var harness = new Harness(Receipt(Email(), report, unread));
        harness.Reader.Recognise(report);
        harness.Reader.Unavailable(unread);

        Assert.Equal(
            OriginalReportRecognitionResult.Unreadable,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Empty(harness.Store.Recorded);
    }

    [Fact]
    public async Task TheMessageAndItsPhotographsAreNeverRead()
    {
        var email = Email();
        var photograph = new IntakeAssetRecord(
            Guid.NewGuid(), "photo.jpg", "photo.jpg", "image/jpeg",
            IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment,
            10, new string('b', 64), "photo-key", null, null, 1600, 1200);
        var report = Pdf("report.pdf");
        var harness = new Harness(Receipt(email, photograph, report));
        harness.Reader.Recognise(report);

        await harness.Sut.ExecuteAsync(CaseId, harness.Receipt);

        Assert.Equal([report.Id], harness.Store.AskedFor);
        Assert.Equal([report.Id], harness.Reader.Read);
    }

    [Fact]
    public async Task NothingIsReadWhenTheCaseDoesNotAwaitItsReport()
    {
        var report = Pdf("report.pdf");
        var harness = new Harness(Receipt(report), awaiting: false);
        harness.Reader.Recognise(report);

        Assert.Equal(
            OriginalReportRecognitionResult.NotApplicable,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Empty(harness.Reader.Read);
        Assert.Empty(harness.Store.Recorded);
    }

    [Fact]
    public async Task AReceiptWithOnlyAMessageAsksNothingOfTheCase()
    {
        var harness = new Harness(Receipt(Email()));

        Assert.Equal(
            OriginalReportRecognitionResult.NotApplicable,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Null(harness.Store.AskedFor);
    }

    [Fact]
    public async Task ACaseEditorDefersTheRecordingForARetry()
    {
        var report = Pdf("report.pdf");
        var harness = new Harness(Receipt(report));
        harness.Reader.Recognise(report);
        harness.Store.Failure = new IntakeDependencyUnavailableException("The Case is being edited.");

        await Assert.ThrowsAsync<IntakeDependencyUnavailableException>(
            () => harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
    }

    [Fact]
    public async Task AnUnfiledCandidateOnACaseThatAwaitsItsReportIsAwaitingFiling()
    {
        var report = Pdf("report.pdf");
        var harness = new Harness(Receipt(report));
        harness.Store.AnythingFiled = false;
        harness.Reader.Recognise(report);

        Assert.Equal(
            OriginalReportRecognitionResult.AwaitingFiling,
            await harness.Sut.ExecuteAsync(CaseId, harness.Receipt));
        Assert.Empty(harness.Reader.Read);
        Assert.Empty(harness.Store.Recorded);
    }

    private static IntakeAssetRecord Email() => new(
        Guid.NewGuid(), "source", "message.eml", "message/rfc822",
        IntakeAssetKind.Source, IntakeAssetDisposition.Source,
        10, new string('c', 64), "message-key", null, null, null, null);

    private static IntakeAssetRecord Pdf(string fileName) => new(
        Guid.NewGuid(), fileName, fileName, "application/pdf",
        IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment,
        10, new string('d', 64), $"{fileName}-key", null, null, null, null);

    private static IntakeReceipt Receipt(params IntakeAssetRecord[] assets)
    {
        var id = Guid.NewGuid();
        var source = assets[0];
        return new(
            id,
            source.FileName,
            source.MediaType,
            source.ContentLength,
            source.ContentHash,
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, id.ToString("N")),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            IntakeDecision.NeedsSorting,
            "Associated with the Case.",
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
            assets,
            ManualLinkedCaseId: CaseId,
            ManualAssociationVersion: 0,
            ManualAssociationActorKind: ActorKind.SystemWorker);
    }

    private sealed class Harness
    {
        public Harness(IntakeReceipt receipt, bool awaiting = true)
        {
            Receipt = receipt;
            Store = new(awaiting);
            Sut = new RecogniseFiledOriginalReport(Store, Reader);
        }

        public IntakeReceipt Receipt { get; }
        public RecordingStore Store { get; }
        public RecordingReader Reader { get; } = new();
        public RecogniseFiledOriginalReport Sut { get; }
    }

    private sealed class RecordingStore(bool awaiting) : IRecogniseOriginalReportStore
    {
        public Dictionary<Guid, FiledOriginalReportCandidate> Filed { get; } = [];
        public IReadOnlyCollection<Guid>? AskedFor { get; private set; }
        public List<(RecordRecognisedOriginalReport Command, OriginalReportReading Reading)> Recorded { get; } = [];
        public Exception? Failure { get; set; }

        /// <summary>Whether the asked-for files are on the Case yet; false models custody still in flight.</summary>
        public bool AnythingFiled { get; set; } = true;

        public Task<FiledOriginalReportCandidates> FindAwaitingCandidatesAsync(
            Guid caseId, Guid receiptId, IReadOnlyCollection<FiledOriginalReportLookup> assets,
            CancellationToken cancellationToken = default)
        {
            AskedFor = assets.Select(asset => asset.IntakeAssetId).ToArray();
            if (!awaiting)
            {
                return Task.FromResult(new FiledOriginalReportCandidates(false, []));
            }

            if (!AnythingFiled)
            {
                return Task.FromResult(new FiledOriginalReportCandidates(true, []));
            }

            foreach (var asset in assets)
            {
                Filed[asset.IntakeAssetId] = new(asset.IntakeAssetId, Guid.NewGuid(), Guid.NewGuid());
            }

            return Task.FromResult(new FiledOriginalReportCandidates(
                true,
                assets.Select(asset => Filed[asset.IntakeAssetId]).ToArray()));
        }

        public Task<OriginalReportRecorded?> RecordRecognisedAsync(
            RecordRecognisedOriginalReport command, OriginalReportReading reading,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Recorded.Add((command, reading));
            return Task.FromResult<OriginalReportRecorded?>(
                new(command.CaseId, command.DocumentOccurrenceId, "report.pdf", 5));
        }
    }

    private sealed class RecordingReader : IReadOriginalReport
    {
        private readonly HashSet<Guid> _recognised = [];
        private readonly HashSet<Guid> _unavailable = [];

        public List<Guid> Read { get; } = [];
        public Dictionary<Guid, OriginalReportReading> Readings { get; } = [];

        public void Recognise(IntakeAssetRecord asset)
        {
            _recognised.Add(asset.Id);
            Readings[asset.Id] = new(
                asset.ContentHash, "Connexus Vehicle Assessors", "2026-03-09", "unroadworthy", "repairable", false);
        }

        public void Unavailable(IntakeAssetRecord asset) => _unavailable.Add(asset.Id);

        public Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
            Guid receiptId, IntakeAssetRecord asset, CancellationToken cancellationToken)
        {
            Read.Add(asset.Id);
            return Task.FromResult(
                _unavailable.Contains(asset.Id) ? new OriginalReportRecognition(OriginalReportRecognitionOutcome.Unavailable)
                : _recognised.Contains(asset.Id)
                    ? new OriginalReportRecognition(OriginalReportRecognitionOutcome.Recognised, Readings[asset.Id])
                    : new OriginalReportRecognition(OriginalReportRecognitionOutcome.NotRecognised));
        }

        public Task<OriginalReportReading?> ForIntakeAsync(
            Guid receiptId, Guid standaloneAuditEvidenceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OriginalReportReading?> ForDocumentAsync(
            ActionActor actor, Guid caseId, Guid occurrenceId, Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
