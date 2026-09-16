using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

public sealed class SupplyAuditOriginalReportTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2031, 9, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly byte[] ReportBytes = [0x25, 0x50, 0x44, 0x46];

    [Fact]
    public async Task ACompleteUnambiguousReportIsAttachedAndQueuesTheReceiptForReevaluation()
    {
        var receipt = AuditReceipt();
        var item = WaitingItem(receipt.Id);
        var mutation = new RecordingMutations();
        var reader = new RecordingReader("The vehicle is repairable.");
        var sut = Sut(item, receipt, reader, mutation);

        var result = await sut.ExecuteAsync(Request(item, receipt));

        Assert.Equal(receipt.Id, result.ReceiptId);
        Assert.False(result.IsDuplicate);
        var probe = Assert.Single(mutation.Probes);
        Assert.Equal(item.Id, probe.UnidentifiedItemId);
        Assert.Equal(item.Version, probe.ExpectedItemVersion);
        Assert.Equal(receipt.Version, probe.ExpectedReceiptVersion);
        var attach = Assert.Single(mutation.Attaches);
        Assert.Equal(receipt.Id, attach.ReceiptId);
        Assert.Equal(item.Version, attach.ExpectedItemVersion);
        Assert.Equal(receipt.Version, attach.ExpectedReceiptVersion);
        Assert.Equal("supplied original report: original-report.pdf", attach.SourceLabel);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(ReportBytes)), attach.ContentHash);
        Assert.Equal(ReportBytes, attach.Content.ToArray());
        Assert.Equal("supplied-original-report", Assert.Single(reader.Sources).SourceIdentity.ExternalReceiptToken[..24]);
    }

    [Theory]
    [InlineData(IntakeSourceReadStatus.Unsupported, false, false, "The vehicle is repairable.")]
    [InlineData(IntakeSourceReadStatus.Readable, true, false, "The vehicle is repairable.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, true, "The vehicle is repairable.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, false, "The vehicle is repairable and total loss.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, false, "The assessment is pending.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, false, "The vehicle is not repairable.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, false, "The vehicle is not a total loss.")]
    [InlineData(IntakeSourceReadStatus.Readable, false, false, "The vehicle is unrepairable.")]
    public async Task AReportWithoutOneCompleteOutcomeIsRefusedBeforeMutation(
        IntakeSourceReadStatus status,
        bool incomplete,
        bool requiresOcr,
        string text)
    {
        var receipt = AuditReceipt();
        var item = WaitingItem(receipt.Id);
        var mutations = new RecordingMutations();
        var sut = Sut(item, receipt, new RecordingReader(status, incomplete, requiresOcr, text), mutations);

        var error = await Assert.ThrowsAsync<SuppliedOriginalReportRefusalException>(
            () => sut.ExecuteAsync(Request(item, receipt)));

        Assert.Equal(SupplyAuditOriginalReport.RefusalMessage, error.Message);
        Assert.Empty(mutations.Attaches);
    }

    [Fact]
    public async Task AnActorWithoutCaseworkAuthorityCannotReadOrAttachTheReport()
    {
        var receipt = AuditReceipt();
        var item = WaitingItem(receipt.Id);
        var mutations = new RecordingMutations();
        var reader = new RecordingReader("Repairable");
        var sut = Sut(item, receipt, reader, mutations);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.ExecuteAsync(
            Request(item, receipt) with { Actor = ActionActor.SystemWorker("unauthorised-supplier") }));

        Assert.Empty(mutations.Probes);
        Assert.Empty(reader.Sources);
        Assert.Empty(mutations.Attaches);
    }

    [Fact]
    public async Task StaleItemOrReceiptStopsBeforeReadingOrMutation()
    {
        foreach (var staleItem in new[] { true, false })
        {
            var receipt = AuditReceipt() with { Version = staleItem ? 7 : 8 };
            var item = WaitingItem(receipt.Id) with { Version = staleItem ? 4 : 3 };
            var mutations = new RecordingMutations();
            var reader = new RecordingReader("The vehicle is repairable.");
            var sut = Sut(item, receipt, reader, mutations);
            var request = Request(item, receipt) with
            {
                ExpectedItemVersion = staleItem ? item.Version - 1 : item.Version,
                ExpectedReceiptVersion = staleItem ? receipt.Version : receipt.Version - 1
            };

            await Assert.ThrowsAsync<IntakeVersionConflictException>(() => sut.ExecuteAsync(request));

            Assert.Empty(reader.Sources);
            Assert.Empty(mutations.Attaches);
        }
    }

    [Fact]
    public async Task ReplayWinsBeforeCurrentStateOrSourceRead()
    {
        var receipt = AuditReceipt();
        var item = WaitingItem(receipt.Id);
        var mutations = new RecordingMutations
        {
            Replay = new(receipt.Id, Guid.NewGuid(), true)
        };
        var reader = new RecordingReader("The vehicle is repairable.");
        var sut = Sut(item, receipt, reader, mutations);

        var result = await sut.ExecuteAsync(Request(item, receipt));

        Assert.True(result.IsDuplicate);
        Assert.Empty(reader.Sources);
        Assert.Empty(mutations.Attaches);
    }

    [Fact]
    public async Task AChangedPayloadUnderTheSameOperationKeyConflictsBeforeSourceRead()
    {
        var receipt = AuditReceipt();
        var item = WaitingItem(receipt.Id);
        var mutations = new RecordingMutations { ProbeFailure = new IntakeOperationConflictException() };
        var reader = new RecordingReader("The vehicle is repairable.");
        var sut = Sut(item, receipt, reader, mutations);

        await Assert.ThrowsAsync<IntakeOperationConflictException>(() => sut.ExecuteAsync(Request(item, receipt)));

        Assert.Empty(reader.Sources);
        Assert.Empty(mutations.Attaches);
    }

    [Theory]
    [InlineData("The vehicle is repairable.", AuditAssessment.Repairable)]
    [InlineData("The vehicle is a total loss.", AuditAssessment.TotalLoss)]
    public void OutcomePolicyReadsOnlyOneLiteralOutcome(string text, AuditAssessment expected)
    {
        var read = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.PdfContent, "original report, page 1", text)], [], [], false);

        var outcome = AuditOriginalReportOutcomePolicy.Evaluate(read, "supplied original report: original-report.pdf");

        Assert.Equal(expected, outcome?.Assessment);
    }

    private static SupplyAuditOriginalReport Sut(
        UnidentifiedItem item,
        IntakeReceipt receipt,
        RecordingReader reader,
        RecordingMutations mutations) =>
        new(new SingleItemStore(item), new SingleReceiptQueries(receipt), reader, mutations, TimeProvider.System);

    private static SupplyAuditOriginalReportRequest Request(UnidentifiedItem item, IntakeReceipt receipt) => new(
        item.Id, item.Version, receipt.Version, Staff, "supply-original-report-1", "original-report.pdf",
        "application/pdf", ReportBytes);

    private static UnidentifiedItem WaitingItem(Guid receiptId) => new(
        Guid.NewGuid(), 42, "U42", UnidentifiedOrigin.Receipt(receiptId),
        UnidentifiedReasonCode.AuditOriginalReportMissing,
        "The Audit instruction arrived without the original report.", UnidentifiedState.Open,
        ReceivedAtUtc, null, ActionActor.SystemWorker("intake-processing"), null, null, null, null, null, 3);

    private static IntakeReceipt AuditReceipt() => new(
        Guid.NewGuid(), "audit.eml", "message/rfc822", 500, new string('0', 64),
        new(IntakeSourceChannel.Mailbox, new string('1', 32)), ReceivedAtUtc, ReceivedAtUtc,
        IntakeDecision.NeedsSorting, "Waiting for the original report.", [], [], null, [], null, null,
        false, "reader", "1", null, null, Version: 7,
        MailClassificationDecision: MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "audit"), [],
            "Exactly one accepted classification predicate family matched.", "qdos_mail_classification", 8,
            CaseType.Audit, standaloneAuditReport: null));

    private sealed class RecordingReader : IIntakeSourceReader
    {
        private readonly IntakeSourceReadResult _result;

        public RecordingReader(string text) : this(IntakeSourceReadStatus.Readable, false, false, text) { }

        public RecordingReader(IntakeSourceReadStatus status, bool incomplete, bool requiresOcr, string text) =>
            _result = new(status,
                status == IntakeSourceReadStatus.Readable
                    ? [new(IntakeEvidenceSource.PdfContent, "original report, page 1", text)]
                    : [], [], [], requiresOcr, IsIncomplete: incomplete);

        public List<IntakeSource> Sources { get; } = [];

        public Task<IntakeSourceReadResult> ReadAsync(IntakeSource source, CancellationToken cancellationToken)
        {
            Sources.Add(source);
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingMutations : IIntakeMutationStore
    {
        public List<ProbeSuppliedOriginalReportReplayRequest> Probes { get; } = [];
        public List<AttachSuppliedOriginalReportRequest> Attaches { get; } = [];
        public AttachSuppliedOriginalReportResult? Replay { get; init; }
        public Exception? ProbeFailure { get; init; }

        public Task<AttachSuppliedOriginalReportResult?> ProbeSuppliedOriginalReportReplayAsync(
            ProbeSuppliedOriginalReportReplayRequest request, CancellationToken cancellationToken)
        {
            Probes.Add(request);
            return ProbeFailure is null
                ? Task.FromResult(Replay)
                : Task.FromException<AttachSuppliedOriginalReportResult?>(ProbeFailure);
        }

        public Task<AttachSuppliedOriginalReportResult> AttachSuppliedOriginalReportAsync(
            AttachSuppliedOriginalReportRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            Attaches.Add(request);
            return Task.FromResult(new AttachSuppliedOriginalReportResult(request.ReceiptId, Guid.NewGuid(), false));
        }

        public Task<IntakeReceipt> ResolveAsync(ResolveIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeReceipt> ScheduleReevaluationAsync(ReevaluateIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeReceipt> ScheduleOcrRetryAsync(RetryIntakeOcrRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkAsync(LinkIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReverseLinkAsync(ReverseIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AutoLinkAsync(AutomaticIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SingleItemStore(UnidentifiedItem item) : IUnidentifiedStore
    {
        public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UnidentifiedItem?>(id == item.Id ? item : null);
        public Task<UnidentifiedRegisterResult> RegisterAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedReasonRefreshResult> RefreshReasonAsync(RefreshUnidentifiedReasonRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedResolveResult> ResolveAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedItem?> GetByOriginAsync(UnidentifiedOrigin origin, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(UnidentifiedState? state = UnidentifiedState.Open, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(UnidentifiedMediaKind? mediaKind, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(Guid unidentifiedItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SingleReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<IntakeReceipt?>(id == receipt.Id ? receipt : null);
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeListPage> ListAsync(IntakeDecision? decision, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeAssetRecord?> GetAssetAsync(Guid receiptId, Guid assetId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
