using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Core.Tests.ProviderApi;

public sealed class ProviderSubmissionTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid PrincipalId = Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b");
    private static readonly Guid OtherPrincipalId = Guid.Parse("7a1b2c3d-0000-4000-8000-000000000001");
    private const string KeyId = "AAAAAAAAAAAAAAAA";
    private const string PrincipalCode = "QDOS";
    private static readonly PrincipalCredentialAuthentication Active =
        new(PrincipalId, KeyId, PrincipalCredentialState.Active);
    private static readonly PrincipalCredentialAuthentication Paused =
        new(PrincipalId, KeyId, PrincipalCredentialState.Paused);

    private static ProviderSubmissionFile File(
        int ordinal,
        byte value = 1,
        DocumentSemanticRole? role = null) =>
        new(ordinal, $"instruction-{ordinal}.pdf", "application/pdf", new byte[] { value, 2, 3 }, role);

    private static ProviderInstruction Instruction(
        ProviderInstructionKind kind = ProviderInstructionKind.Inspection,
        AuditAssessment? verdict = null,
        string? principal = null) =>
        new(
            kind,
            verdict,
            principal,
            ClaimNumber: "12345/1",
            ClaimantName: "Alex Mercer",
            VehicleRegistration: "AB12CDE");

    private static ProviderSubmissionRequest Request(
        PrincipalCredentialAuthentication credential,
        string key = "order-1",
        ProviderInstruction? instruction = null,
        byte body = 9,
        params ProviderSubmissionFile[] files) =>
        new(
            credential,
            key,
            instruction ?? Instruction(),
            files.Length == 0 ? [File(0)] : files,
            Encoding.UTF8.GetBytes($"{{\"body\":{body}}}"),
            "trace-1");

    private static SubmitProviderInstruction Submit(
        FakeStore store,
        FakeIntakeSubmission intake,
        FakeHistory? history = null)
    {
        history ??= new FakeHistory();
        history.Store = store;
        return new(store, intake, history, new FixedTime());
    }

    private static ReconcileProviderSubmissions Reconcile(
        FakeStore store,
        FakeIntakeSubmission intake,
        FakeHistory history)
    {
        history.Store = store;
        store.HistoryEntries.Clear();
        store.HistoryEntries.AddRange(history.Entries);
        return new(store, intake, history, new FixedTime());
    }

    private static ProviderSubmissionRecord Submission(
        Guid id,
        DateTimeOffset? receivedAtUtc = null,
        Guid? stagedReceiptId = null) =>
        new(
            id,
            PrincipalId,
            KeyId,
            $"key-{id:N}",
            "12345/1",
            receivedAtUtc ?? Now - ReconcileProviderSubmissions.AcceptHistoryGracePeriod - TimeSpan.FromSeconds(1),
            Instruction(),
            stagedReceiptId);

    private static IntakeStagedReceipt StagedReceipt(Guid submissionId, Guid stagedReceiptId) =>
        new(
            stagedReceiptId,
            ProviderInstructionPolicy.SourceFileName,
            ProviderInstructionPolicy.SourceMediaType,
            1,
            "HASH",
            new(IntakeSourceChannel.ProviderApi, ProviderSubmissionPolicy.SubmissionToken(submissionId)),
            Now - ReconcileProviderSubmissions.AcceptHistoryGracePeriod - TimeSpan.FromSeconds(1),
            "provider:0f149cac-e1d4-4a57-925f-7c35d33d7f5b",
            $"staged:{stagedReceiptId:N}",
            Now);

    private static ActionHistoryEntry History(Guid submissionId, string outcome) =>
        new(
            Guid.NewGuid(),
            ProviderSubmissionPolicy.ActionHistoryAggregateType,
            submissionId.ToString("D"),
            "Submitted",
            ActionActor.Provider(PrincipalId),
            Now,
            outcome,
            $"request:{submissionId:N}");

    [Fact]
    public async Task DeclaredSubmissionIsRetainedAsOneSourceOnTheProviderChannel()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();

        var receipt = await Submit(store, intake, history).ExecuteAsync(
            Request(Active, files: [File(0), File(1, value: 7)]),
            CancellationToken.None);

        // One submission is one receipt: the request as sent, carrying its files
        // the way an e-mail carries its attachments.
        var source = Assert.Single(intake.Sources);
        Assert.Equal(IntakeSourceChannel.ProviderApi, source.SourceIdentity.Channel);
        Assert.Equal(ProviderInstructionPolicy.SourceFileName, source.FileName);
        Assert.Equal(ProviderInstructionPolicy.SourceMediaType, source.MediaType);
        Assert.Equal(receipt.SubmissionId.ToString("N"), source.SourceIdentity.ExternalReceiptToken);
        Assert.False(receipt.Replayed);

        // The declaration is retained with the submission, so processing never
        // has to re-read the body to know what was instructed.
        var stored = Assert.Single(store.Records.Values);
        Assert.Equal("12345/1", stored.Instruction?.ClaimNumber);
        Assert.Equal(ProviderInstructionKind.Inspection, stored.Instruction?.Kind);

        Assert.Equal(2, receipt.Files.Count);
        Assert.All(receipt.Files, file => Assert.False(file.IsDuplicate));
        Assert.Equal(ActorKind.Provider, Assert.Single(history.Entries).Actor.Kind);
    }

    [Fact]
    public async Task PausedCredentialIsRefusedBeforeAnythingIsRetained()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        var error = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => Submit(store, intake).ExecuteAsync(Request(Paused), CancellationToken.None));

        Assert.Equal(ProviderSubmissionError.CredentialPaused, error.Error);
        Assert.Empty(store.Records);
        Assert.Empty(intake.Sources);
    }

    [Fact]
    public async Task ABodyNamingAnotherPrincipalIsRefusedRatherThanRedirected()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        var error = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => Submit(store, intake).ExecuteAsync(
                Request(Active, instruction: Instruction(principal: "OTHER")),
                CancellationToken.None));

        Assert.Equal(ProviderSubmissionError.PrincipalMismatch, error.Error);
        Assert.Empty(store.Records);
        Assert.Empty(intake.Sources);
    }

    [Fact]
    public async Task ABodyNamingItsOwnPrincipalIsAccepted()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        var receipt = await Submit(store, intake).ExecuteAsync(
            Request(Active, instruction: Instruction(principal: " qdos ")),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, receipt.SubmissionId);
    }

    [Fact]
    public async Task ReplayReturnsTheSameSubmissionAndADifferentBodyFailsClosed()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var submit = Submit(store, intake);

        var first = await submit.ExecuteAsync(Request(Active), CancellationToken.None);
        var replay = await submit.ExecuteAsync(Request(Active), CancellationToken.None);

        Assert.Equal(first.SubmissionId, replay.SubmissionId);
        Assert.True(replay.Replayed);
        Assert.Single(store.Records);

        var error = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Active, body: 42), CancellationToken.None));
        Assert.Equal(ProviderSubmissionError.IdempotencyKeyConflict, error.Error);
    }

    [Fact]
    public async Task LosingAConcurrentInsertResolvesToTheWinnersSubmission()
    {
        var store = new FakeStore { ConflictOnce = true };
        var intake = new FakeIntakeSubmission();

        var receipt = await Submit(store, intake).ExecuteAsync(Request(Active), CancellationToken.None);

        Assert.Single(store.Records);
        Assert.Equal(store.Records.Keys.Single(), receipt.SubmissionId);
    }

    [Fact]
    public async Task TheEnvelopeIsBoundedByFileCountBySingleFileAndByTotal()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var submit = Submit(store, intake);

        var tooMany = Enumerable
            .Range(0, IntakeEnvelopeLimits.MaximumBatchFileCount + 1)
            .Select(ordinal => File(ordinal, (byte)(ordinal % 250 + 1)))
            .ToArray();
        Assert.Equal(
            ProviderSubmissionError.EnvelopeExceeded,
            (await Assert.ThrowsAsync<ProviderSubmissionException>(
                () => submit.ExecuteAsync(Request(Active, files: tooMany), CancellationToken.None))).Error);

        var oversizeFile = new ProviderSubmissionFile(
            0,
            "big.pdf",
            "application/pdf",
            new byte[IntakeEnvelopeLimits.MaximumContentLength + 1]);
        Assert.Equal(
            ProviderSubmissionError.EnvelopeExceeded,
            (await Assert.ThrowsAsync<ProviderSubmissionException>(
                () => submit.ExecuteAsync(Request(Active, files: [oversizeFile]), CancellationToken.None))).Error);

        // Four files each inside the per-file bound still exceed the envelope,
        // which is the bound base64 in one request body actually costs.
        var eachUnderTheFileBound = Enumerable
            .Range(0, 4)
            .Select(ordinal => new ProviderSubmissionFile(
                ordinal,
                $"big-{ordinal}.pdf",
                "application/pdf",
                new byte[IntakeEnvelopeLimits.MaximumContentLength]))
            .ToArray();
        Assert.Equal(
            ProviderSubmissionError.EnvelopeExceeded,
            (await Assert.ThrowsAsync<ProviderSubmissionException>(
                () => submit.ExecuteAsync(
                    Request(Active, files: eachUnderTheFileBound),
                    CancellationToken.None))).Error);

        Assert.Empty(intake.Sources);
    }

    [Fact]
    public async Task AnAuditMustAttachItsOriginalReportAndOnlyAnAuditCarriesAVerdict()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var submit = Submit(store, intake);

        // The declared verdict decides the reference (operator, 2026-08-28), but
        // the Engineer still needs the report they are auditing.
        var missingReport = await Assert.ThrowsAsync<ProviderInstructionValidationException>(
            () => submit.ExecuteAsync(
                Request(
                    Active,
                    instruction: Instruction(ProviderInstructionKind.Audit, AuditAssessment.Repairable)),
                CancellationToken.None));
        Assert.Equal("files", missingReport.Field);

        var noVerdict = await Assert.ThrowsAsync<ProviderInstructionValidationException>(
            () => submit.ExecuteAsync(
                Request(Active, instruction: Instruction(ProviderInstructionKind.Audit)),
                CancellationToken.None));
        Assert.Equal("originalReportVerdict", noVerdict.Field);

        // Inspection + Audit audits Collision Engineers' own report, so it has
        // no incoming report and no verdict to state (FRD-01 § Case types).
        var strayVerdict = await Assert.ThrowsAsync<ProviderInstructionValidationException>(
            () => submit.ExecuteAsync(
                Request(
                    Active,
                    instruction: Instruction(ProviderInstructionKind.AuditReport, AuditAssessment.TotalLoss)),
                CancellationToken.None));
        Assert.Equal("originalReportVerdict", strayVerdict.Field);

        // Two files claiming the role is as unusable as none: both would take
        // the one fixed label and the downstream single-match lookup would fail
        // the accepted intake rather than name the field.
        var twoReports = await Assert.ThrowsAsync<ProviderInstructionValidationException>(
            () => submit.ExecuteAsync(
                Request(
                    Active,
                    instruction: Instruction(ProviderInstructionKind.Audit, AuditAssessment.Repairable),
                    files:
                    [
                        File(0, role: DocumentSemanticRole.AuditReport),
                        File(1, value: 7, role: DocumentSemanticRole.AuditReport)
                    ]),
                CancellationToken.None));
        Assert.Equal("files", twoReports.Field);

        var accepted = await submit.ExecuteAsync(
            Request(
                Active,
                instruction: Instruction(ProviderInstructionKind.Audit, AuditAssessment.Repairable),
                files: [File(0), File(1, value: 7, role: DocumentSemanticRole.AuditReport)]),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, accepted.SubmissionId);
    }

    [Fact]
    public async Task ResultIsReadableWhilePausedAndNeverAcrossPrincipals()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var receipt = await Submit(store, intake).ExecuteAsync(Request(Active), CancellationToken.None);

        var status = new FakeStatus();
        var staged = intake.StagedIds.Single();
        status.Statuses[staged] = new(
            staged,
            ProviderInstructionPolicy.SourceFileName,
            Now,
            QueuedIntakeStatusKind.Complete,
            ProcessedReceiptId: null,
            FailureCode: null);
        var result = new GetProviderSubmissionResult(store, status, status);

        var paused = await result.ExecuteAsync(Paused, receipt.SubmissionId, CancellationToken.None);
        Assert.NotNull(paused);
        Assert.Equal(QueuedIntakeStatusKind.Complete, paused.Status);

        // Another Principal's submission and one that does not exist are the
        // same answer: nothing (FRD-09 fails closed on cross-principal reads).
        var foreign = new PrincipalCredentialAuthentication(
            OtherPrincipalId, KeyId, PrincipalCredentialState.Active);
        Assert.Null(await result.ExecuteAsync(foreign, receipt.SubmissionId, CancellationToken.None));
        Assert.Null(await result.ExecuteAsync(Active, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptRecoveryRepairsTheRowAndAcceptedHistoryAfterIntakeRetention()
    {
        var submissionId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId, stagedReceiptId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(0, result.Failures);
        Assert.Equal(stagedReceiptId, store.Records[submissionId].StagedReceiptId);
        var accepted = Assert.Single(history.Entries);
        Assert.Equal("Accepted", accepted.Outcome);
        Assert.Equal(ProviderSubmissionPolicy.OperationKey(submissionId), accepted.CorrelationId);
        Assert.Equal(ActorKind.Provider, accepted.Actor.Kind);
        Assert.Equal(PrincipalId.ToString("D"), accepted.Actor.SubjectId);
    }

    [Fact]
    public async Task AcceptRecoveryWritesAcceptedWhenOnlyTheBackReferenceWasWritten()
    {
        var submissionId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId, stagedReceiptId: stagedReceiptId);
        intake.AddStagedReceipt(StagedReceipt(submissionId, stagedReceiptId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(0, store.RecordStagedReceiptCalls.GetValueOrDefault(submissionId));
        Assert.Equal(stagedReceiptId, store.Records[submissionId].StagedReceiptId);
        Assert.Equal("Accepted", Assert.Single(history.Entries).Outcome);
    }

    [Fact]
    public async Task AcceptRecoveryAddsAcceptedWhenAReplayOnlyWroteReplayed()
    {
        var submissionId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId, stagedReceiptId: stagedReceiptId);
        intake.AddStagedReceipt(StagedReceipt(submissionId, stagedReceiptId));
        history.Add(History(submissionId, "Replayed"));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(["Replayed", "Accepted"], history.Entries.Select(entry => entry.Outcome));
    }

    [Fact]
    public async Task AcceptRecoveryLeavesABareReservationUntouched()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Equal(0, result.Failures);
        Assert.Null(store.Records[submissionId].StagedReceiptId);
        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task AcceptRecoveryDoesNothingForAnInlineCompletedSubmission()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        var receipt = await Submit(store, intake, history).ExecuteAsync(
            Request(Active),
            CancellationToken.None);

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Single(history.Entries);
        Assert.Equal("Accepted", history.Entries[0].Outcome);
        Assert.Equal(1, store.RecordStagedReceiptCalls[receipt.SubmissionId]);
    }

    [Fact]
    public async Task AcceptRecoveryDefersAJustCreatedSubmissionInsideTheGraceWindow()
    {
        var submissionId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId, receivedAtUtc: Now);
        intake.AddStagedReceipt(StagedReceipt(submissionId, stagedReceiptId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Null(store.Records[submissionId].StagedReceiptId);
        Assert.Empty(history.Entries);
        Assert.Empty(intake.SourceLookups);
    }

    [Fact]
    public async Task AcceptRecoveryCountsARecoverableFailureAndContinuesTheBatch()
    {
        var failedId = Guid.NewGuid();
        var repairedId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[failedId] = Submission(failedId);
        store.Records[repairedId] = Submission(repairedId);
        intake.AddStagedReceipt(StagedReceipt(failedId, Guid.NewGuid()));
        intake.AddStagedReceipt(StagedReceipt(repairedId, Guid.NewGuid()));
        store.RecordFailures[failedId] = new IOException("temporary database failure");

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(2, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(1, result.Failures);
        Assert.Null(store.Records[failedId].StagedReceiptId);
        Assert.NotNull(store.Records[repairedId].StagedReceiptId);
        Assert.Equal(repairedId.ToString("D"), Assert.Single(history.Entries).AggregateId);
    }

    [Fact]
    public async Task AcceptRecoveryPropagatesANonRecoverableFailure()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId, Guid.NewGuid()));
        store.RecordFailures[submissionId] = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Reconcile(store, intake, history).ExecuteAsync(50));
    }

    [Fact]
    public async Task ProviderSubmissionResultChangesFromReceivedAfterAcceptRecovery()
    {
        var submissionId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId, stagedReceiptId));
        var status = new FakeStatus();
        status.Statuses[stagedReceiptId] = new(
            stagedReceiptId,
            ProviderInstructionPolicy.SourceFileName,
            Now,
            QueuedIntakeStatusKind.Processing,
            ProcessedReceiptId: null,
            FailureCode: null);
        var getResult = new GetProviderSubmissionResult(store, status, status);

        var before = await getResult.ExecuteAsync(Active, submissionId, CancellationToken.None);
        Assert.Equal(QueuedIntakeStatusKind.Received, before?.Status);

        await Reconcile(store, intake, history).ExecuteAsync(50);

        var after = await getResult.ExecuteAsync(Active, submissionId, CancellationToken.None);
        Assert.Equal(QueuedIntakeStatusKind.Processing, after?.Status);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeStore : IProviderSubmissionStore
    {
        public Dictionary<Guid, ProviderSubmissionRecord> Records { get; } = [];
        public List<ActionHistoryEntry> HistoryEntries { get; } = [];
        public Dictionary<Guid, Exception> RecordFailures { get; } = [];
        public Dictionary<Guid, int> RecordStagedReceiptCalls { get; } = [];
        public bool ConflictOnce { get; set; }

        public Task CreateAsync(ProviderSubmissionRecord record, CancellationToken cancellationToken)
        {
            if (ConflictOnce)
            {
                // The winner of the race is another row under the same key,
                // keyed by its own id as every other row in this fake is.
                ConflictOnce = false;
                var winner = record with { Id = Guid.NewGuid() };
                Records[winner.Id] = winner;
                throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }
            if (Records.Values.Any(item =>
                    item.PrincipalId == record.PrincipalId && item.IdempotencyKey == record.IdempotencyKey))
            {
                throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }

            Records[record.Id] = record;
            return Task.CompletedTask;
        }

        public Task<ProviderSubmissionRecord?> FindByIdempotencyKeyAsync(
            Guid principalId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Records.Values.SingleOrDefault(item =>
                item.PrincipalId == principalId && item.IdempotencyKey == idempotencyKey));

        public Task<ProviderSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Records.GetValueOrDefault(id));

        public Task<string?> FindPrincipalCodeAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(principalId == PrincipalId ? PrincipalCode : null);

        public Task RecordStagedReceiptAsync(
            Guid submissionId, Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            RecordStagedReceiptCalls[submissionId] =
                RecordStagedReceiptCalls.GetValueOrDefault(submissionId) + 1;
            if (RecordFailures.TryGetValue(submissionId, out var exception))
            {
                throw exception;
            }
            if (Records.TryGetValue(submissionId, out var record))
            {
                if (record.StagedReceiptId != stagedReceiptId)
                {
                    Records[submissionId] = record with { StagedReceiptId = stagedReceiptId };
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProviderSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
            int maximumItems,
            CancellationToken cancellationToken)
        {
            var accepted = AcceptedSubmissionIds();
            IReadOnlyList<ProviderSubmissionAcceptCandidate> candidates = Records.Values
                .Where(record => record.StagedReceiptId is null || !accepted.Contains(record.Id))
                .OrderBy(record => record.ReceivedAtUtc)
                .ThenBy(record => record.Id)
                .Take(maximumItems)
                .Select(record => new ProviderSubmissionAcceptCandidate(
                    record.Id,
                    record.PrincipalId,
                    record.ReceivedAtUtc,
                    record.StagedReceiptId,
                    accepted.Contains(record.Id)))
                .ToArray();
            return Task.FromResult(candidates);
        }

        public Task<ProviderSubmissionAcceptCandidate?> GetAcceptRecoveryCandidateAsync(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            var accepted = AcceptedSubmissionIds();
            var record = Records.GetValueOrDefault(submissionId);
            return Task.FromResult(
                record is null
                    ? null
                    : new ProviderSubmissionAcceptCandidate(
                        record.Id,
                        record.PrincipalId,
                        record.ReceivedAtUtc,
                        record.StagedReceiptId,
                        accepted.Contains(record.Id)));
        }

        private HashSet<Guid> AcceptedSubmissionIds() => HistoryEntries
            .Where(entry =>
                entry.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType
                && entry.Outcome == "Accepted")
            .Select(entry => Guid.Parse(entry.AggregateId))
            .ToHashSet();
    }

    /// <summary>
    /// One retained source per identity, with the same identity-conflict rule
    /// the real receiver enforces: the same token with different bytes is a
    /// visible conflict, never a second receipt.
    /// </summary>
    private sealed class FakeIntakeSubmission : IIntakeSubmission, IIntakeWorkStore
    {
        private readonly Dictionary<string, (IntakeStagedReceipt Receipt, string Hash)> retained = new(StringComparer.Ordinal);

        public List<IntakeSource> Sources { get; } = [];
        public List<IntakeSourceIdentity> SourceLookups { get; } = [];

        public IReadOnlyCollection<Guid> StagedIds => retained.Values.Select(item => item.Receipt.Id).ToArray();

        public void AddStagedReceipt(IntakeStagedReceipt receipt) =>
            retained[receipt.SourceIdentity.ExternalReceiptToken] = (receipt, receipt.SourceHash);

        public Task<ReceivedIntake> ExecuteAsync(
            IntakeSource source, string operationKey, CancellationToken cancellationToken = default)
        {
            var token = source.SourceIdentity.ExternalReceiptToken;
            var hash = ProviderSubmissionPolicy.Sha256(source.Content);
            if (retained.TryGetValue(token, out var existing))
            {
                if (!string.Equals(existing.Hash, hash, StringComparison.Ordinal))
                {
                    throw new IntakeSourceIdentityConflictException();
                }

                return Task.FromResult(new ReceivedIntake(existing.Receipt.Id, IsDuplicate: true));
            }

            var id = Guid.NewGuid();
            retained[token] = (
                new IntakeStagedReceipt(
                    id,
                    source.FileName,
                    source.MediaType,
                    source.Content.Length,
                    hash,
                    source.SourceIdentity,
                    source.ReceivedAtUtc,
                    source.Actor,
                    $"staged:{id:N}",
                    Now),
                hash);
            Sources.Add(source);
            return Task.FromResult(new ReceivedIntake(id, IsDuplicate: false));
        }

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken)
        {
            SourceLookups.Add(sourceIdentity);
            return Task.FromResult(
                retained.Values
                    .Select(item => item.Receipt)
                    .SingleOrDefault(receipt => receipt.SourceIdentity == sourceIdentity));
        }

        public Task<ReceivedIntake> ReceiveAsync(
            IntakeStagedReceipt receipt,
            string operationKey,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            Guid stagedReceiptId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<IntakeWorkItem?> FindWorkItemAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
            Guid stagedReceiptId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<IntakeEvaluationRevision> CompleteProcessingAsync(
            Guid workItemId,
            string leaseToken,
            Guid processedReceiptId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task RetryProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            bool terminal,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task MarkPoisonedAsync(
            Guid stagedReceiptId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<int> RecoverInterruptedWorkAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset staleDispatchedBeforeUtc,
            int maximumItems,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task ScheduleReevaluationAsync(
            Guid stagedReceiptId,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw UnsupportedWorkStoreCall();

        private static NotSupportedException UnsupportedWorkStoreCall() =>
            new("The provider accept-recovery tests only use source-identity lookup.");

    }

    private sealed class FakeHistory : IActionHistoryWriter
    {
        public List<ActionHistoryEntry> Entries { get; } = [];
        public FakeStore? Store { get; set; }

        public void Add(ActionHistoryEntry entry)
        {
            Entries.Add(entry);
            Store?.HistoryEntries.Add(entry);
        }

        public Task AppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken)
        {
            Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStatus : IQueuedIntakeStatusQueries, IIntakeReceiptQueries
    {
        public Dictionary<Guid, QueuedIntakeStatus> Statuses { get; } = [];
        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public Task<QueuedIntakeStatus?> GetAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Statuses.GetValueOrDefault(stagedReceiptId));

        Task<IntakeReceipt?> IIntakeReceiptQueries.GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipts.GetValueOrDefault(id));

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId, Guid assetId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
