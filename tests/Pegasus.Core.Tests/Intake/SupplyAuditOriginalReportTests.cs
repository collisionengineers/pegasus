using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The staff "Supply original report" command: the item gates, the receipt
/// gates, the content pre-check, retention, and the queued re-evaluation that
/// the supply commits together with the attachment.
/// </summary>
public sealed class SupplyAuditOriginalReportTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2031, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ProcessedAtUtc = new(2031, 9, 10, 8, 5, 0, TimeSpan.Zero);

    private static readonly ActionActor Staff = ActionActor.Staff(
        Guid.NewGuid(),
        [StaffRole.Engineer]);

    private static readonly byte[] ReportBytes = [0x25, 0x50, 0x44, 0x46];

    private static SupplyAuditOriginalReportRequest Request(
        Guid itemId,
        long version = 3,
        ReadOnlyMemory<byte>? content = null,
        string? operationKey = null) => new(
        itemId,
        version,
        Staff,
        operationKey ?? $"supply-test:{Guid.NewGuid():N}",
        "original-report.pdf",
        "application/pdf",
        content ?? ReportBytes);

    [Fact]
    public async Task AnUnambiguousReportIsRetainedAttachedAndReevaluationQueued()
    {
        var receipt = AuditReceiptWithoutReport();
        var item = OpenAuditItem(receipt.Id);
        var store = new StubUnidentifiedStore(item);
        var receipts = new StubReceiptQueries(receipt);
        var reader = new StubReader(readableReport: "The vehicle is a total loss.");
        var artifacts = new StubArtifactStore();
        var mutations = new StubMutationStore();
        var sut = new SupplyAuditOriginalReport(
            store,
            receipts,
            new StubAuditEvidenceQueries(null),
            reader,
            artifacts,
            mutations,
            new FixedTimeProvider(ProcessedAtUtc));

        var updated = await sut.ExecuteAsync(Request(item.Id, version: item.Version));

        var attach = Assert.Single(mutations.Requests);
        Assert.Equal(receipt.Id, attach.ReceiptId);
        Assert.Equal(receipt.Version, attach.ExpectedVersion);
        Assert.Equal("original-report.pdf", attach.FileName);
        Assert.Equal("supplied original report: original-report.pdf", attach.SourceLabel);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ReportBytes)),
            attach.ContentHash,
            ignoreCase: false);
        Assert.Equal(attach.ContentHash, artifacts.StoredKey.Split('/')[^1], ignoreCase: true);

        // The command returns what the mutation wrote back: the receipt,
        // moved to its queued re-evaluation state.
        Assert.Equal(receipt.Id, updated.Id);
        Assert.Equal(IntakeDecision.BlockedIntake, updated.Decision);

        // The item's history names the operator's act, keyed off the same
        // operation key so a replay notes nothing twice.
        Assert.Equal($"{attach.OperationKey}:note", store.Note?.OperationKey);
        Assert.Equal("Original report added: original-report.pdf", store.Note?.Reason);
    }

    [Fact]
    public async Task AnAmbiguousReportIsRefusedAndNothingIsRetained()
    {
        var receipt = AuditReceiptWithoutReport();
        var item = OpenAuditItem(receipt.Id);
        var artifacts = new StubArtifactStore();
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: "repairable and also a total loss"),
            artifacts,
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        var error = await Assert.ThrowsAsync<SuppliedOriginalReportRefusalException>(
            () => sut.ExecuteAsync(Request(item.Id)));

        Assert.Equal(SupplyAuditOriginalReport.RefusalMessage, error.Message);
        Assert.Empty(artifacts.StoredKeys);
    }

    [Fact]
    public async Task AReportWithoutAnyLiteralIsRefused()
    {
        var receipt = AuditReceiptWithoutReport();
        var item = OpenAuditItem(receipt.Id);
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: "The assessor's conclusion is on the next page."),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        await Assert.ThrowsAsync<SuppliedOriginalReportRefusalException>(
            () => sut.ExecuteAsync(Request(item.Id)));
    }

    [Fact]
    public async Task AnUnreadableUploadIsRefused()
    {
        var receipt = AuditReceiptWithoutReport();
        var item = OpenAuditItem(receipt.Id);
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: null),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        await Assert.ThrowsAsync<SuppliedOriginalReportRefusalException>(
            () => sut.ExecuteAsync(Request(item.Id)));
    }

    [Fact]
    public async Task AnItemThatIsNotAnOpenReportLessAuditIsRefused()
    {
        // Resolved, wrong reason, or a grouped origin — none of them accept a
        // supplied report through this command.
        foreach (var item in new[]
                 {
                     OpenAuditItem() with { State = UnidentifiedState.Resolved },
                     OpenAuditItem() with { ReasonCode = UnidentifiedReasonCode.NoUsableIdentification },
                     OpenAuditItem() with { Origin = UnidentifiedOrigin.SubmissionGroup(Guid.NewGuid()) }
                 })
        {
            var sut = new SupplyAuditOriginalReport(
                new StubUnidentifiedStore(item),
                new StubReceiptQueries(AuditReceiptWithoutReport()),
                new StubAuditEvidenceQueries(null),
                new StubReader(readableReport: "The vehicle is repairable."),
                new StubArtifactStore(),
                new StubMutationStore(),
                new FixedTimeProvider(ProcessedAtUtc));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExecuteAsync(Request(item.Id)));
        }
    }

    [Fact]
    public async Task AReceiptThatAlreadyHasEvidenceIsRefused()
    {
        var receipt = AuditReceiptWithoutReport();
        var item = OpenAuditItem(receipt.Id);
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(new(
                Guid.NewGuid(), receipt.Id, Guid.NewGuid(), AuditAssessment.Repairable,
                Guid.Empty, ProcessedAtUtc, "recorded", 2, false)),
            new StubReader(readableReport: "The vehicle is repairable."),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(Request(item.Id)));
    }

    [Fact]
    public async Task AReceiptThatIsNotAReportLessAuditIsRefused()
    {
        var receipt = AuditReceiptWithoutReport() with
        {
            MailClassificationDecision = null
        };
        var item = OpenAuditItem(receipt.Id);
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: "The vehicle is repairable."),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(Request(item.Id)));
    }

    [Fact]
    public async Task AReceiptAssociatedWithACaseIsRefused()
    {
        var receipt = AuditReceiptWithoutReport() with { AcceptedCaseId = Guid.NewGuid() };
        var item = OpenAuditItem(receipt.Id);
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(item),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: "The vehicle is repairable."),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(Request(item.Id)));
    }

    [Fact]
    public async Task NonStaffActorsAreRefused()
    {
        var receipt = AuditReceiptWithoutReport();
        var sut = new SupplyAuditOriginalReport(
            new StubUnidentifiedStore(OpenAuditItem(receipt.Id)),
            new StubReceiptQueries(receipt),
            new StubAuditEvidenceQueries(null),
            new StubReader(readableReport: "The vehicle is repairable."),
            new StubArtifactStore(),
            new StubMutationStore(),
            new FixedTimeProvider(ProcessedAtUtc));
        var request = Request(receipt.Id) with
        {
            Actor = ActionActor.SystemWorker("worker")
        };

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(request));
    }

    private static UnidentifiedItem OpenAuditItem(Guid? originId = null) => new(
        Guid.NewGuid(),
        42,
        "U42",
        UnidentifiedOrigin.Receipt(originId ?? Guid.NewGuid()),
        UnidentifiedReasonCode.AuditOriginalReportMissing,
        "The Audit instruction arrived without the original report it audits. Add that report to continue.",
        UnidentifiedState.Open,
        ReceivedAtUtc,
        null,
        ActionActor.SystemWorker("intake-processing"),
        null,
        null,
        null,
        null,
        null,
        3);

    private static IntakeReceipt AuditReceiptWithoutReport() => new(
        Guid.NewGuid(),
        "audit.eml",
        "message/rfc822",
        500,
        new string('0', 64),
        new(IntakeSourceChannel.Mailbox, new string('1', 32)),
        ReceivedAtUtc,
        ReceivedAtUtc,
        IntakeDecision.NeedsSorting,
        "The Audit instruction arrived without the original report it audits. Add that report to continue.",
        [],
        [],
        null,
        [],
        null,
        null,
        false,
        "reader",
        "1",
        null,
        null,
        Version: 7,
        MailClassificationDecision: MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "audit"),
            [],
            "Exactly one accepted classification predicate family matched.",
            "qdos_mail_classification",
            8,
            CaseType.Audit,
            standaloneAuditReport: null));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubReader(string? readableReport) : IIntakeSourceReader
    {
        public Task<IntakeSourceReadResult> ReadAsync(
            IntakeSource source,
            CancellationToken cancellationToken) => Task.FromResult(
            readableReport is null
                ? new IntakeSourceReadResult(
                    IntakeSourceReadStatus.Unsupported,
                    [],
                    [],
                    [],
                    false,
                    "unsupported_source",
                    "The file is unsupported.")
                : new IntakeSourceReadResult(
                    IntakeSourceReadStatus.Readable,
                    [new(IntakeEvidenceSource.PdfContent, "report, page 1", readableReport)],
                    [],
                    [],
                    false));
    }

    private sealed class StubArtifactStore : IIntakeArtifactStore
    {
        private readonly List<string> storedKeys = [];

        public string StoredKey => storedKeys[^1];

        public IReadOnlyList<string> StoredKeys => storedKeys;

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            var key = $"sha256/{contentHash[..2]}/{contentHash}";
            storedKeys.Add(key);
            return Task.FromResult(key);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    private sealed class StubMutationStore : IIntakeMutationStore
    {
        public List<AttachSuppliedOriginalReportRequest> Requests { get; } = [];

        public AttachSuppliedOriginalReportRequest? Single => Requests.Count == 1 ? Requests[0] : null;

        public Task<IntakeReceipt> AttachSuppliedOriginalReportAsync(
            AttachSuppliedOriginalReportRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new IntakeReceipt(
                request.ReceiptId,
                "audit.eml",
                "message/rfc822",
                500,
                new string('0', 64),
                new(IntakeSourceChannel.Mailbox, new string('1', 32)),
                ReceivedAtUtc,
                ProcessedAtUtc,
                IntakeDecision.BlockedIntake,
                "A policy re-evaluation of the retained source is queued.",
                [],
                [],
                null,
                [],
                "reevaluation_pending",
                null,
                false,
                "reader",
                "1",
                null,
                null,
                Version: 8));
        }

        public Task<IntakeReceipt> ResolveAsync(
            ResolveIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleReevaluationAsync(
            ReevaluateIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LinkAsync(
            LinkIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReverseLinkAsync(
            ReverseIntakeLinkRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AutoLinkAsync(
            AutomaticIntakeLinkRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubUnidentifiedStore(UnidentifiedItem item) : IUnidentifiedStore
    {
        public UnidentifiedHistoryEntry? Note { get; private set; }

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(item.Id == id ? item : null);

        public Task<UnidentifiedHistoryEntry> AppendNoteAsync(
            Guid unidentifiedItemId,
            string note,
            ActionActor actor,
            string operationKey,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            Note = new UnidentifiedHistoryEntry(
                Guid.NewGuid(),
                unidentifiedItemId,
                UnidentifiedState.Open,
                UnidentifiedState.Open,
                actor,
                occurredAtUtc,
                note,
                operationKey,
                null,
                null,
                null);
            return Task.FromResult(Note);
        }

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UnidentifiedHistoryEntry>>([]);
    }

    private sealed class StubReceiptQueries(IntakeReceipt? receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(receipt is not null && receipt.Id == id ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubAuditEvidenceQueries(StandaloneAuditEvidence? evidence)
        : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(evidence is not null && evidence.IntakeReceiptId == intakeReceiptId
                ? evidence
                : null);
    }
}
