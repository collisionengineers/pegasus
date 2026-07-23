using System.Security.Cryptography;
using CollisionSpike.Core.Intake.Qdos;

namespace CollisionSpike.Core.Tests.Intake.Qdos;

public sealed class ProcessQdosIntakeTests
{
    private static readonly DateTimeOffset ProcessedAtUtc = new(2031, 4, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedAtUtc = new(2030, 12, 31, 16, 45, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(IntakeSourceReadStatus.Unsupported, QdosIntakeDecision.Unsupported, "unsupported_test", "The test source is unsupported.")]
    [InlineData(IntakeSourceReadStatus.TechnicalFailure, QdosIntakeDecision.TechnicalFailure, "technical_test", "The test source could not be read.")]
    public async Task UnreadableResultIsPersistedWithItsFailure(
        IntakeSourceReadStatus status,
        QdosIntakeDecision expectedDecision,
        string failureCode,
        string failureReason)
    {
        var readResult = new IntakeSourceReadResult(
            status,
            [],
            [],
            [new("reader_issue", "The reader supplied diagnostic context.", QdosEvidenceSource.FileName)],
            false,
            failureCode,
            failureReason);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(expectedDecision, draft.Decision);
        Assert.Equal(failureCode, draft.FailureCode);
        Assert.Equal(failureReason, draft.FailureReason);
        Assert.Contains(draft.Evidence, evidence =>
            evidence.Signal == "reader_issue" &&
            evidence.Finding == QdosEvidenceFinding.Information);
        Assert.Equal(expectedDecision, result.Decision);
        Assert.Equal(failureCode, result.FailureCode);
    }

    [Fact]
    public async Task ReaderExceptionIsSanitisedBeforePersistence()
    {
        const string sensitiveDetail = "storage-account-secret-detail";
        var reader = new StubReader((_, _) => throw new InvalidOperationException(sensitiveDetail));
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(QdosIntakeDecision.TechnicalFailure, draft.Decision);
        Assert.Equal("source_reader_failure", draft.FailureCode);
        Assert.Equal("The uploaded source could not be read because of a technical failure.", draft.FailureReason);
        Assert.DoesNotContain(sensitiveDetail, draft.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveDetail, result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReaderCancellationIsPropagatedWithoutPersistence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new StubReader((_, token) => throw new OperationCanceledException(token));
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(CreateSource(), cancellation.Token));

        Assert.Empty(store.Drafts);
    }

#pragma warning disable CA2201 // These tests verify that runtime-reserved terminal exceptions are never swallowed.
    [Fact]
    public void ExceptionPolicyRejectsTerminalExceptionsAndAcceptsRecoverableExceptions()
    {
        Assert.False(QdosIntakeExceptionPolicy.IsRecoverable(new OperationCanceledException()));
        Assert.False(QdosIntakeExceptionPolicy.IsRecoverable(new OutOfMemoryException()));
        Assert.False(QdosIntakeExceptionPolicy.IsRecoverable(new AccessViolationException()));
        Assert.True(QdosIntakeExceptionPolicy.IsRecoverable(new InvalidOperationException()));
    }

    [Fact]
    public async Task ReaderOutOfMemoryIsPropagatedWithoutPersistence()
    {
        var reader = new StubReader((_, _) => throw new OutOfMemoryException());
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await Assert.ThrowsAsync<OutOfMemoryException>(() => sut.ExecuteAsync(CreateSource()));

        Assert.Empty(store.Drafts);
    }
#pragma warning restore CA2201

    [Fact]
    public async Task StoreCancellationIsPropagatedWithoutRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new RecordingStore((_, token) => throw new OperationCanceledException(token));
        var sut = CreateSut(new StubReader(Readable()), store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(CreateSource(), cancellation.Token));

        Assert.Single(store.Drafts);
    }

    [Fact]
    public async Task OcrRequirementWithoutConfirmingContentIsPersistedForReview()
    {
        var readResult = Readable(requiresOcr: true);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(QdosIntakeDecision.OcrRequired, draft.Decision);
        Assert.Equal("ocr_required", draft.FailureCode);
        Assert.Empty(draft.Fields);
        Assert.Empty(draft.MissingFields);
        Assert.Equal(QdosIntakeDecision.OcrRequired, result.Decision);
    }

    [Fact]
    public async Task WeakTransportMarkerAloneDoesNotConfirmInstructionContent()
    {
        var readResult = Readable(
            transportEvidence: [new(QdosEvidenceSource.FileName, "QDOS-upload.pdf")]);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(QdosIntakeDecision.NeedsSorting, draft.Decision);
        var evidence = Assert.Single(draft.Evidence);
        Assert.Equal(QdosEvidenceStrength.Weak, evidence.Strength);
        Assert.Equal(QdosEvidenceFinding.SupportsQdos, evidence.Finding);
        Assert.Equal("qdos-transport-marker", evidence.Signal);
        Assert.Equal(QdosIntakeDecision.NeedsSorting, result.Decision);
    }

    [Fact]
    public async Task PersistenceDraftUsesSafeBasenameHashClockActorAndSourceIdentity()
    {
        byte[] content = [0x10, 0x20, 0x30, 0x40];
        var source = new QdosIntakeSource(
            Path.Combine("untrusted", "nested", "selected.pdf"),
            "application/pdf",
            content,
            ReceivedAtUtc,
            "operator-123",
            new(IntakeSourceChannel.ManualUpload, "11111111111111111111111111111111"));
        var reader = new StubReader(Readable());
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await sut.ExecuteAsync(source);

        Assert.Equal("selected.pdf", Assert.Single(reader.Sources).FileName);
        var draft = Assert.Single(store.Drafts);
        Assert.Equal("selected.pdf", draft.SourceFileName);
        Assert.Equal("application/pdf", draft.MediaType);
        Assert.Equal(content.Length, draft.SourceLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)), draft.SourceHash);
        Assert.Equal(ReceivedAtUtc, draft.ReceivedAtUtc);
        Assert.Equal(ProcessedAtUtc, draft.ProcessedAtUtc);
        Assert.Equal("operator-123", draft.Actor);
        Assert.Equal(source.SourceIdentity, draft.SourceIdentity);
    }

    [Fact]
    public async Task ConfirmedContentCreatesTypedReviewDraftWithoutCaseSemantics()
    {
        var content = new IntakeContentFragment(
            QdosEvidenceSource.DocumentContent,
            "controlled protocol fixture",
            """
            QDOS instruction
            Claimant Name: Review Claimant
            Claim Number: PROTOCOL-001
            Vehicle Registration: AB12 CDE
            Vehicle Make: Example Make
            Vehicle Model: Example Model
            Vehicle Mileage: 12,345 miles
            Accident Circumstances: Controlled fixture circumstances
            Date of Incident: 04/03/2031
            Instruction Date: 05/03/2031
            Inspection Address: Image Based Assessment
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        var typed = Assert.IsType<QdosTypedDraft>(draft.TypedDraft);
        Assert.Equal(QdosIntakeDecision.DraftReady, result.Decision);
        Assert.Equal("QDOS", typed.PrincipalCode);
        Assert.Equal("Review Claimant", typed.ClaimantName);
        Assert.Equal("PROTOCOL-001", typed.ClaimNumber);
        Assert.Equal("AB12CDE", typed.VehicleRegistration);
        Assert.Equal("Example Make", typed.VehicleMake);
        Assert.Equal("Example Model", typed.VehicleModel);
        Assert.Equal(12345L, typed.VehicleMileage);
        Assert.Equal("Controlled fixture circumstances", typed.AccidentCircumstances);
        Assert.Equal(new DateOnly(2031, 3, 4), typed.DateOfIncident);
        Assert.Equal(new DateOnly(2031, 3, 5), typed.InstructionDate);
        Assert.Equal("Image Based Assessment", typed.InspectionAddress);
        Assert.Equal(
            "AB12 CDE",
            Assert.Single(draft.Fields, field => field.Name == "Vehicle registration").SuggestedValue);
        Assert.Equal(
            "12,345 miles",
            Assert.Single(draft.Fields, field => field.Name == "Vehicle mileage").SuggestedValue);
        Assert.Equal(
            "04/03/2031",
            Assert.Single(draft.Fields, field => field.Name == "Date of incident").SuggestedValue);
    }

    [Theory]
    [InlineData("Claim Number | PROTOCOL-BLANK-001")]
    [InlineData("Claim Number PROTOCOL-BLANK-001")]
    public async Task BlankFieldDoesNotConsumeTheNextFieldLabel(string claimNumberLine)
    {
        var content = new IntakeContentFragment(
            QdosEvidenceSource.DocumentContent,
            "controlled blank-field fixture",
            $$"""
            QDOS instruction
            Claimant Name:
            {{claimNumberLine}}
            Vehicle Registration: AB12 CDE
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal(QdosIntakeDecision.DraftReady, result.Decision);
        Assert.Contains("Claimant name", result.MissingFields);
        var claimantName = Assert.Single(result.Fields, field => field.Name == "Claimant name");
        Assert.Null(claimantName.SuggestedValue);
        Assert.Empty(claimantName.Candidates);
        Assert.False(claimantName.HasConflict);
        Assert.Null(Assert.IsType<QdosTypedDraft>(result.TypedDraft).ClaimantName);
        Assert.Equal(
            "PROTOCOL-BLANK-001",
            Assert.Single(result.Fields, field => field.Name == "Claim number").SuggestedValue);
    }

    [Fact]
    public async Task FieldValueMayRemainOnTheNextLine()
    {
        var content = new IntakeContentFragment(
            QdosEvidenceSource.DocumentContent,
            "controlled next-line fixture",
            """
            QDOS instruction
            Claimant Name:
            Review Claimant
            Claim Number: PROTOCOL-NEXT-LINE-001
            Vehicle Registration: AB12 CDE
            """);
        var sut = CreateSut(new StubReader(Readable(content: [content])), new RecordingStore());

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal("Review Claimant", Assert.IsType<QdosTypedDraft>(result.TypedDraft).ClaimantName);
        Assert.DoesNotContain("Claimant name", result.MissingFields);
    }

    [Fact]
    public async Task InvalidAndConflictingTypedValuesRetainCandidatesWithNullTypedValues()
    {
        var content = new IntakeContentFragment(
            QdosEvidenceSource.EmailBody,
            "controlled email body",
            """
            QDOS instruction
            Claim Number: PROTOCOL-INVALID
            Vehicle Registration: AB12 CDE
            Vehicle Mileage: unknown pending review
            Date of Incident: 04/03/2031
            Date of Incident: 05/03/2031
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var typed = Assert.IsType<QdosTypedDraft>(result.TypedDraft);
        Assert.Null(typed.VehicleMileage);
        Assert.Null(typed.DateOfIncident);
        var mileage = Assert.Single(result.Fields, field => field.Name == "Vehicle mileage");
        Assert.Equal("unknown pending review", mileage.SuggestedValue);
        Assert.Single(mileage.Candidates);
        Assert.Equal("controlled email body", mileage.Candidates[0].SourceLabel);
        var incidentDate = Assert.Single(result.Fields, field => field.Name == "Date of incident");
        Assert.True(incidentDate.HasConflict);
        Assert.Null(incidentDate.SuggestedValue);
        Assert.Equal(2, incidentDate.Candidates.Count);
    }

    [Fact]
    public async Task OverlongStringsAndInvalidRegistrationRemainFullCandidatesButTypedValuesAreNull()
    {
        var claimant = new string('C', 301);
        var claimNumber = new string('N', 101);
        var make = new string('K', 101);
        var model = new string('M', 101);
        var circumstances = new string('A', 2001);
        var address = new string('I', 1001);
        const string registration = "INVALID!* REGISTRATION";
        var content = new IntakeContentFragment(
            QdosEvidenceSource.DocumentContent,
            "controlled overlong document",
            $"""
            QDOS instruction
            Claimant Name: {claimant}
            Claim Number: {claimNumber}
            Vehicle Registration: {registration}
            Vehicle Make: {make}
            Vehicle Model: {model}
            Accident Circumstances: {circumstances}
            Inspection Address: {address}
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var typed = Assert.IsType<QdosTypedDraft>(result.TypedDraft);
        Assert.Null(typed.ClaimantName);
        Assert.Null(typed.ClaimNumber);
        Assert.Null(typed.VehicleRegistration);
        Assert.Null(typed.VehicleMake);
        Assert.Null(typed.VehicleModel);
        Assert.Null(typed.AccidentCircumstances);
        Assert.Null(typed.InspectionAddress);
        foreach (var (fieldName, expectedValue) in new[]
                 {
                     ("Claimant name", claimant),
                     ("Claim number", claimNumber),
                     ("Vehicle registration", registration),
                     ("Vehicle make", make),
                     ("Vehicle model", model),
                     ("Accident circumstances", circumstances),
                     ("Inspection address", address)
                 })
        {
            var field = Assert.Single(result.Fields, item => item.Name == fieldName);
            Assert.Equal(expectedValue, field.SuggestedValue);
            var candidate = Assert.Single(field.Candidates);
            Assert.Equal(expectedValue, candidate.Value);
            Assert.Equal(QdosEvidenceSource.DocumentContent, candidate.Source);
            Assert.Equal("controlled overlong document", candidate.SourceLabel);
        }
    }

    private static ProcessQdosIntake CreateSut(IQdosIntakeSourceReader reader, IQdosIntakeStore store) =>
        new(reader, store, new FixedTimeProvider(ProcessedAtUtc));

    private static QdosIntakeSource CreateSource() =>
        new(
            "selected.pdf",
            "application/pdf",
            new byte[] { 0x01 },
            ReceivedAtUtc,
            "operator",
            new(IntakeSourceChannel.ManualUpload, "22222222222222222222222222222222"));

    private static IntakeSourceReadResult Readable(
        bool requiresOcr = false,
        IReadOnlyList<IntakeTransportEvidence>? transportEvidence = null,
        IReadOnlyList<IntakeContentFragment>? content = null) =>
        new(
            IntakeSourceReadStatus.Readable,
            content ?? [],
            transportEvidence ?? [],
            [],
            requiresOcr);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubReader : IQdosIntakeSourceReader
    {
        private readonly Func<QdosIntakeSource, CancellationToken, Task<IntakeSourceReadResult>> read;

        public StubReader(IntakeSourceReadResult result)
            : this((_, _) => Task.FromResult(result))
        {
        }

        public StubReader(Func<QdosIntakeSource, CancellationToken, Task<IntakeSourceReadResult>> read)
        {
            this.read = read;
        }

        public List<QdosIntakeSource> Sources { get; } = [];

        public Task<IntakeSourceReadResult> ReadAsync(
            QdosIntakeSource source,
            CancellationToken cancellationToken)
        {
            Sources.Add(source);
            return read(source, cancellationToken);
        }
    }

    private sealed class RecordingStore : IQdosIntakeStore
    {
        private readonly Func<QdosIntakeDraft, CancellationToken, Task<QdosIntakeRecord>> store;

        public RecordingStore()
            : this((draft, _) => Task.FromResult(RecordFrom(draft)))
        {
        }

        public RecordingStore(Func<QdosIntakeDraft, CancellationToken, Task<QdosIntakeRecord>> store)
        {
            this.store = store;
        }

        public List<QdosIntakeDraft> Drafts { get; } = [];

        public QdosIntakeRecord? ExistingRecord { get; set; }

        public Task<QdosIntakeRecord?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                ExistingRecord?.SourceIdentity == sourceIdentity
                    ? ExistingRecord
                    : null);

        public Task<QdosIntakeRecord> StoreAsync(QdosIntakeDraft draft, CancellationToken cancellationToken)
        {
            Drafts.Add(draft);
            return store(draft, cancellationToken);
        }

        public static QdosIntakeRecord RecordFrom(QdosIntakeDraft draft) =>
            new(
                new Guid("eb239fbc-cfd4-46c9-87dd-c784404ff3f6"),
                draft.SourceFileName,
                draft.MediaType,
                draft.SourceLength,
                draft.SourceHash,
                draft.SourceIdentity,
                draft.ReceivedAtUtc,
                draft.Decision,
                draft.DecisionReason,
                draft.Evidence,
                draft.Fields,
                draft.TypedDraft,
                draft.MissingFields,
                draft.FailureCode,
                draft.FailureReason,
                false);
    }
}
