using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.PrincipalApi;

public sealed class PrincipalSubmissionTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid PrincipalId = Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b");
    private static readonly Guid OtherPrincipalId = Guid.Parse("7a1b2c3d-0000-4000-8000-000000000001");
    private const string KeyId = "AAAAAAAAAAAAAAAA";
    private static readonly PrincipalCredentialAuthentication Active =
        new(PrincipalId, KeyId, PrincipalCredentialState.Active);
    private static readonly PrincipalCredentialAuthentication Paused =
        new(PrincipalId, KeyId, PrincipalCredentialState.Paused);

    private static object File(int index, byte value = 1) => new
    {
        fileName = $"instruction-{index}.pdf",
        contentBase64 = Convert.ToBase64String(new byte[] { value, 2, 3 })
    };

    private static string Body(
        string caseType = "inspection",
        string? verdict = null,
        string claimNumber = "12345/1",
        object[]? files = null) =>
        JsonSerializer.Serialize(new
        {
            caseType,
            originalReportVerdict = verdict,
            claimNumber,
            claimant = new { name = "Alex Mercer" },
            vehicle = new { registration = "AB12CDE" },
            files = files ?? [File(0)]
        });

    private static PrincipalSubmissionRequest Request(
        PrincipalCredentialAuthentication credential,
        string key = "order-1",
        string? body = null) =>
        new(credential, key, Encoding.UTF8.GetBytes(body ?? Body()), "trace-1");

    private static SubmitPrincipalInstruction Submit(
        FakeStore store,
        FakeIntakeSubmission intake,
        FakeHistory? history = null)
    {
        history ??= new FakeHistory();
        history.Store = store;
        return new(store, intake, new AcceptingAdmission(), history, new FixedTime());
    }

    private static ReconcilePrincipalSubmissions Reconcile(
        FakeStore store,
        FakeIntakeSubmission intake,
        FakeHistory history)
    {
        history.Store = store;
        store.Intake = intake;
        store.HistoryEntries.Clear();
        store.HistoryEntries.AddRange(history.Entries);
        return new(store, history, new FixedTime());
    }

    private static PrincipalSubmissionRecord Submission(Guid id, DateTimeOffset? receivedAtUtc = null) =>
        new(
            id,
            PrincipalId,
            receivedAtUtc ?? Now - ReconcilePrincipalSubmissions.AcceptHistoryGracePeriod - TimeSpan.FromSeconds(1),
            PrincipalInstructionJson.Parse(Encoding.UTF8.GetBytes(Body())).Instruction);

    private static IntakeStagedReceipt StagedReceipt(Guid submissionId) =>
        new(
            Guid.NewGuid(),
            PrincipalInstructionPolicy.SourceFileName,
            PrincipalInstructionPolicy.SourceMediaType,
            1,
            "HASH",
            PrincipalSubmissionPolicy.SourceIdentity(submissionId),
            Now - ReconcilePrincipalSubmissions.AcceptHistoryGracePeriod - TimeSpan.FromSeconds(1),
            "principal:0f149cac-e1d4-4a57-925f-7c35d33d7f5b",
            $"staged:{submissionId:N}",
            Now);

    private static ActionHistoryEntry History(Guid submissionId, string outcome) =>
        new(
            Guid.NewGuid(),
            PrincipalSubmissionPolicy.ActionHistoryAggregateType,
            submissionId.ToString("D"),
            "Submitted",
            ActionActor.Principal(PrincipalId),
            Now,
            outcome,
            $"request:{submissionId:N}");

    [Fact]
    public async Task DeclaredSubmissionIsRetainedAsOneSourceOnThePrincipalChannel()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();

        var receipt = await Submit(store, intake, history).ExecuteAsync(
            Request(Active, body: Body(files: [File(0), File(1, value: 7)])),
            CancellationToken.None);

        // One submission is one receipt: the request as sent, carrying its files
        // the way an e-mail carries its attachments.
        var source = Assert.Single(intake.Sources);
        Assert.Equal(PrincipalSubmissionPolicy.SourceIdentity(receipt.SubmissionId), source.SourceIdentity);
        Assert.Equal(PrincipalInstructionPolicy.SourceFileName, source.FileName);
        Assert.Equal(PrincipalInstructionPolicy.SourceMediaType, source.MediaType);
        Assert.Equal(PrincipalSubmissionPolicy.SubmissionId(PrincipalId, "order-1"), receipt.SubmissionId);
        Assert.Equal("12345/1", receipt.PrincipalReference);
        Assert.False(receipt.Replayed);

        // The declaration is retained with the submission, so processing never
        // has to re-read the body to know what was instructed.
        var stored = Assert.Single(store.Records.Values);
        Assert.Equal("12345/1", stored.Instruction.Draft.ClaimNumber);
        Assert.Equal(CaseType.Inspection, stored.Instruction.CaseType);
        Assert.Equal(ActorKind.Principal, Assert.Single(history.Entries).Actor.Kind);
    }

    [Theory]
    [InlineData("inspection", null, CaseType.Inspection)]
    [InlineData("auditreport", null, CaseType.InspectionAndAudit)]
    [InlineData("triage", null, CaseType.Triage)]
    [InlineData("audit", "repairable", CaseType.Audit)]
    public async Task AnInstructionIsAcceptedWithNoFiles(string caseType, string? verdict, CaseType expected)
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        // The files are optional for every kind, an Audit included: the
        // declaration alone is a complete submission (operator, 2026-09-28).
        await Submit(store, intake).ExecuteAsync(
            Request(Active, body: Body(caseType, verdict, files: [])),
            CancellationToken.None);

        Assert.Single(intake.Sources);
        Assert.Equal(expected, Assert.Single(store.Records.Values).Instruction.CaseType);
    }

    [Fact]
    public async Task AnInvalidDeclarationIsRefusedBeforeAnythingIsRetained()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        // The report is optional (operator, 2026-09-28); the verdict is not.
        var error = await Assert.ThrowsAsync<PrincipalInstructionValidationException>(
            () => Submit(store, intake).ExecuteAsync(
                Request(Active, body: Body("audit")),
                CancellationToken.None));

        Assert.Equal("originalReportVerdict", error.Field);
        Assert.Empty(store.Records);
        Assert.Empty(intake.Sources);
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

        // A different declaration and the same declaration with different
        // files are both a different submission under a reused key.
        foreach (var body in new[] { Body(claimNumber: "99999/9"), Body(files: [File(0, value: 42)]) })
        {
            var error = await Assert.ThrowsAsync<PrincipalSubmissionException>(
                () => submit.ExecuteAsync(Request(Active, body: body), CancellationToken.None));
            Assert.Equal(PrincipalSubmissionError.IdempotencyKeyConflict, error.Error);
        }

        Assert.Single(intake.Sources);
    }

    [Fact]
    public async Task OneKeyNamesOneSubmissionPerPrincipal()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var submit = Submit(store, intake);
        var other = new PrincipalCredentialAuthentication(OtherPrincipalId, KeyId, PrincipalCredentialState.Active);

        var mine = await submit.ExecuteAsync(Request(Active), CancellationToken.None);
        var theirs = await submit.ExecuteAsync(Request(other), CancellationToken.None);

        Assert.NotEqual(mine.SubmissionId, theirs.SubmissionId);
        Assert.False(theirs.Replayed);
        Assert.Equal(2, intake.Sources.Count);
    }

    private sealed class AcceptingAdmission : IPrincipalAttachmentAdmission
    {
        public Task RequireSupportedAsync(
            IReadOnlyList<PrincipalSubmissionFile> files,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Before the source is retained only the stored declaration says what a
    /// key was first used for, so a different declaration is refused; the same
    /// declaration is the same submission, whose source is then retained.
    /// </summary>
    [Fact]
    public async Task AFailedRetentionIsCompletedOnlyByTheSameDeclaration()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission { FailBeforeRetainOnce = true };
        var submit = Submit(store, intake);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            submit.ExecuteAsync(Request(Active), CancellationToken.None));
        var reserved = Assert.Single(store.Records.Values);
        Assert.Empty(intake.Sources);

        var conflict = await Assert.ThrowsAsync<PrincipalSubmissionException>(() =>
            submit.ExecuteAsync(Request(Active, body: Body(claimNumber: "99999/9")), CancellationToken.None));
        Assert.Equal(PrincipalSubmissionError.IdempotencyKeyConflict, conflict.Error);
        Assert.Empty(intake.Sources);

        var completed = await submit.ExecuteAsync(
            Request(Active, body: Body(files: [File(0, value: 42)])),
            CancellationToken.None);
        Assert.Equal(reserved.Id, completed.SubmissionId);
        Assert.Single(intake.Sources);
    }

    [Fact]
    public async Task TheEnvelopeIsBoundedBeforeAnythingIsRetained()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();

        var tooMany = Enumerable
            .Range(0, IntakeEnvelopeLimits.MaximumBatchFileCount + 1)
            .Select(index => File(index, (byte)(index + 1)))
            .ToArray();
        var error = await Assert.ThrowsAsync<PrincipalSubmissionException>(
            () => Submit(store, intake).ExecuteAsync(
                Request(Active, body: Body(files: tooMany)),
                CancellationToken.None));

        Assert.Equal(PrincipalSubmissionError.EnvelopeExceeded, error.Error);
        Assert.Empty(store.Records);
        Assert.Empty(intake.Sources);
    }

    [Fact]
    public async Task ResultIsReadableWhilePausedAndNeverAcrossPrincipals()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var receipt = await Submit(store, intake).ExecuteAsync(Request(Active), CancellationToken.None);

        var status = new FakeStatus();
        status.Add(receipt.SubmissionId, QueuedIntakeStatusKind.Complete, processedReceiptId: null);
        var result = new GetPrincipalSubmissionResult(store, status, status, new FakeTriageQueries());

        var paused = await result.ExecuteAsync(Paused, receipt.SubmissionId, CancellationToken.None);
        Assert.NotNull(paused);
        Assert.Equal(QueuedIntakeStatusKind.Complete, paused.Status);
        Assert.Equal("12345/1", paused.PrincipalReference);

        // Another Principal's submission and one that does not exist are the
        // same answer: nothing (FRD-09 fails closed on cross-principal reads).
        var foreign = new PrincipalCredentialAuthentication(
            OtherPrincipalId, KeyId, PrincipalCredentialState.Active);
        Assert.Null(await result.ExecuteAsync(foreign, receipt.SubmissionId, CancellationToken.None));
        Assert.Null(await result.ExecuteAsync(Active, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ASubmissionWhoseSourceWasNeverRetainedReadsAsReceived()
    {
        var store = new FakeStore();
        var submissionId = Guid.NewGuid();
        store.Records[submissionId] = Submission(submissionId);
        var status = new FakeStatus();

        var result = await new GetPrincipalSubmissionResult(store, status, status, new FakeTriageQueries())
            .ExecuteAsync(Active, submissionId, CancellationToken.None);

        Assert.Equal(QueuedIntakeStatusKind.Received, result?.Status);
    }

    /// <summary>
    /// A Triage request becomes a Triage Case, not an accepted instruction, so
    /// its receipt names no current Case; the result returns the Triage
    /// Case's t. reference (decision U).
    /// </summary>
    [Fact]
    public async Task ResultReturnsTheTriageCaseReferenceWhenTheRequestOpenedATriageCase()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var receipt = await Submit(store, intake).ExecuteAsync(Request(Active), CancellationToken.None);
        var status = new FakeStatus();
        var processedId = Guid.NewGuid();
        status.Add(receipt.SubmissionId, QueuedIntakeStatusKind.Complete, processedId);
        status.Receipts[processedId] = new IntakeReceipt(
            processedId,
            PrincipalInstructionPolicy.SourceFileName,
            "application/json",
            1,
            new string('a', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.PrincipalApi, "principal-token"),
            Now,
            Now,
            IntakeDecision.NeedsSorting,
            "A Triage request.",
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
            null);
        var triage = new FakeTriageQueries();
        triage.ByOriginReceipt[processedId] = new TriageSummary(
            Guid.NewGuid(),
            "AB12CDE",
            TriageState.Open,
            AssigneeId: null,
            LinkedInstructionCaseId: null,
            Now,
            Version: 0,
            Reference: "t.QDOS26001",
            PrincipalCode: "QDOS");

        var result = await new GetPrincipalSubmissionResult(store, status, status, triage)
            .ExecuteAsync(Active, receipt.SubmissionId, CancellationToken.None);

        Assert.Equal("t.QDOS26001", result?.CaseReference);
    }

    [Fact]
    public async Task AcceptRecoveryWritesTheAcceptedHistoryAfterIntakeRetention()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(0, result.Failures);
        var accepted = Assert.Single(history.Entries);
        Assert.Equal("Accepted", accepted.Outcome);
        Assert.Equal(PrincipalSubmissionPolicy.AcceptedHistoryId(submissionId), accepted.Id);
        Assert.Equal(PrincipalSubmissionPolicy.OperationKey(submissionId), accepted.CorrelationId);
        Assert.Equal(ActorKind.Principal, accepted.Actor.Kind);
        Assert.Equal(PrincipalId.ToString("D"), accepted.Actor.SubjectId);
        // The accept happened when the submission was received, days before
        // this sweep, and the row says so rather than stating the sweep's own
        // clock and a correlation id no request ever used.
        Assert.Equal(store.Records[submissionId].ReceivedAtUtc, accepted.OccurredAtUtc);
        Assert.NotEqual(Now, accepted.OccurredAtUtc);
        Assert.Contains("accept recovery", accepted.Reason);
    }

    [Fact]
    public async Task AcceptRecoveryAddsAcceptedWhenAReplayOnlyWroteReplayed()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId));
        history.Add(History(submissionId, "Replayed"));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(["Replayed", "Accepted"], history.Entries.Select(entry => entry.Outcome));
        // Permanent history is read in time order: the accept the replay
        // replayed cannot be stamped after it.
        var replayed = history.Entries.Single(entry => entry.Outcome == "Replayed");
        var accepted = history.Entries.Single(entry => entry.Outcome == "Accepted");
        Assert.True(accepted.OccurredAtUtc < replayed.OccurredAtUtc);
    }

    /// <summary>
    /// The sweep's snapshot said the Accepted row was missing; by the time it
    /// wrote, the request it was recovering for had appended its own. One
    /// acceptance, recorded once, with the request's own correlation id.
    /// </summary>
    [Fact]
    public async Task AcceptRecoveryLosesTheRaceToTheRequestsOwnAcceptedRow()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId));
        var reconcile = Reconcile(store, intake, history);
        history.AddAfterSnapshot(History(submissionId, "Accepted") with
        {
            Id = PrincipalSubmissionPolicy.AcceptedHistoryId(submissionId)
        });

        var result = await reconcile.ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Equal(0, result.Failures);
        var accepted = Assert.Single(history.Entries);
        Assert.Equal($"request:{submissionId:N}", accepted.CorrelationId);
    }

    [Fact]
    public async Task AcceptRecoveryNeverSelectsABareReservation()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId);

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Equal(0, result.Failures);
        Assert.Empty(history.Entries);
    }

    /// <summary>
    /// Nothing ever removes a bare reservation, so if one could be selected a
    /// run of them would hold the oldest-first window for good and every
    /// repairable submission behind them would starve.
    /// </summary>
    [Fact]
    public async Task AcceptRecoveryIsNotStarvedByOlderBareReservations()
    {
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        for (var index = 0; index < 60; index++)
        {
            var reservationId = Guid.NewGuid();
            store.Records[reservationId] = Submission(
                reservationId,
                receivedAtUtc: Now - TimeSpan.FromDays(1));
        }

        var submissionId = Guid.NewGuid();
        store.Records[submissionId] = Submission(submissionId);
        intake.AddStagedReceipt(StagedReceipt(submissionId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal("Accepted", Assert.Single(history.Entries).Outcome);
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
        // The identity the sweep would write under, so the two paths collide
        // instead of both recording the acceptance.
        Assert.Equal(
            PrincipalSubmissionPolicy.AcceptedHistoryId(receipt.SubmissionId),
            history.Entries[0].Id);
    }

    [Fact]
    public async Task AcceptRecoveryDefersAJustCreatedSubmissionInsideTheGraceWindow()
    {
        var submissionId = Guid.NewGuid();
        var store = new FakeStore();
        var intake = new FakeIntakeSubmission();
        var history = new FakeHistory();
        store.Records[submissionId] = Submission(submissionId, receivedAtUtc: Now);
        intake.AddStagedReceipt(StagedReceipt(submissionId));

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Repaired);
        Assert.Empty(history.Entries);
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
        intake.AddStagedReceipt(StagedReceipt(failedId));
        intake.AddStagedReceipt(StagedReceipt(repairedId));
        history.Failures[failedId] = new IOException("temporary database failure");

        var result = await Reconcile(store, intake, history).ExecuteAsync(50);

        Assert.Equal(2, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(1, result.Failures);
        // The count alone cannot tell a missing grant from a dropped
        // connection, and the sweep swallows both.
        Assert.Equal("IOException: temporary database failure", result.FirstFailure);
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
        intake.AddStagedReceipt(StagedReceipt(submissionId));
        history.Failures[submissionId] = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Reconcile(store, intake, history).ExecuteAsync(50));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeStore : IPrincipalSubmissionStore
    {
        public Dictionary<Guid, PrincipalSubmissionRecord> Records { get; } = [];
        public List<ActionHistoryEntry> HistoryEntries { get; } = [];
        public FakeIntakeSubmission? Intake { get; set; }

        public Task<PrincipalSubmissionRecord> GetOrCreateAsync(
            PrincipalSubmissionRecord record,
            CancellationToken cancellationToken)
        {
            if (!Records.TryGetValue(record.Id, out var stored))
            {
                stored = Records[record.Id] = record;
            }

            return Task.FromResult(stored);
        }

        public Task<PrincipalSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Records.GetValueOrDefault(id));

        public Task<IReadOnlyList<PrincipalSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
            int maximumItems,
            CancellationToken cancellationToken)
        {
            var accepted = HistoryEntries
                .Where(entry =>
                    entry.AggregateType == PrincipalSubmissionPolicy.ActionHistoryAggregateType
                    && entry.Outcome == "Accepted")
                .Select(entry => Guid.Parse(entry.AggregateId))
                .ToHashSet();
            IReadOnlyList<PrincipalSubmissionAcceptCandidate> candidates = Records.Values
                .Where(record => Intake?.IsRetained(record.Id) == true && !accepted.Contains(record.Id))
                .OrderBy(record => record.ReceivedAtUtc)
                .ThenBy(record => record.Id)
                .Take(maximumItems)
                .Select(record => new PrincipalSubmissionAcceptCandidate(
                    record.Id,
                    record.PrincipalId,
                    record.ReceivedAtUtc))
                .ToArray();
            return Task.FromResult(candidates);
        }
    }

    /// <summary>
    /// One retained source per identity, with the same identity-conflict rule
    /// the real receiver enforces: the same token with different bytes is a
    /// visible conflict, never a second receipt.
    /// </summary>
    private sealed class FakeIntakeSubmission : IIntakeSubmission
    {
        private readonly Dictionary<string, (Guid Id, string Hash)> retained = new(StringComparer.Ordinal);

        public List<IntakeSource> Sources { get; } = [];
        public bool FailBeforeRetainOnce { get; set; }

        public void AddStagedReceipt(IntakeStagedReceipt receipt) =>
            retained[receipt.SourceIdentity.ExternalReceiptToken] = (receipt.Id, receipt.SourceHash);

        public bool IsRetained(Guid submissionId) =>
            retained.ContainsKey(PrincipalSubmissionPolicy.SubmissionToken(submissionId));

        public Task<ReceivedIntake> ExecuteAsync(
            IntakeSource source, string operationKey, CancellationToken cancellationToken = default)
        {
            if (FailBeforeRetainOnce)
            {
                FailBeforeRetainOnce = false;
                throw new InvalidOperationException("Simulated failure after reservation.");
            }
            var token = source.SourceIdentity.ExternalReceiptToken;
            var hash = Convert.ToHexString(SHA256.HashData(source.Content.Span));
            if (retained.TryGetValue(token, out var existing))
            {
                if (!string.Equals(existing.Hash, hash, StringComparison.Ordinal))
                {
                    throw new IntakeSourceIdentityConflictException();
                }

                return Task.FromResult(new ReceivedIntake(existing.Id, IsDuplicate: true));
            }

            var id = Guid.NewGuid();
            retained[token] = (id, hash);
            Sources.Add(source);
            return Task.FromResult(new ReceivedIntake(id, IsDuplicate: false));
        }

        public async Task<ReceivedIntake> ExecuteStreamedAsync(
            StreamedIntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            using var content = await source.OpenContentAsync(cancellationToken);
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            return await ExecuteAsync(
                new(
                    source.FileName,
                    source.MediaType,
                    buffer.ToArray(),
                    source.ReceivedAtUtc,
                    source.Actor,
                    source.SourceIdentity),
                operationKey,
                cancellationToken);
        }
    }

    /// <summary>
    /// Appends like the real writer, including its one constraint: an entry id
    /// already in the stream is refused rather than written twice.
    /// </summary>
    private sealed class FakeHistory : IActionHistoryWriter
    {
        public List<ActionHistoryEntry> Entries { get; } = [];
        public FakeStore? Store { get; set; }
        public Dictionary<Guid, Exception> Failures { get; } = [];

        public void Add(ActionHistoryEntry entry)
        {
            Entries.Add(entry);
            Store?.HistoryEntries.Add(entry);
        }

        /// <summary>
        /// Records a row the sweep's already-taken candidate snapshot does not
        /// know about — the inline request finishing mid-pass.
        /// </summary>
        public void AddAfterSnapshot(ActionHistoryEntry entry) => Entries.Add(entry);

        public Task AppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken)
        {
            Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> TryAppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken)
        {
            if (Failures.TryGetValue(Guid.Parse(entry.AggregateId), out var exception))
            {
                throw exception;
            }
            if (Entries.Any(existing => existing.Id == entry.Id))
            {
                return Task.FromResult(false);
            }

            Add(entry);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeTriageQueries : ITriageQueries
    {
        public Dictionary<Guid, TriageSummary> ByOriginReceipt { get; } = [];

        public Task<IReadOnlyList<TriageSummary>> ListAsync(IReadOnlyCollection<TriageState>? state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(IReadOnlyCollection<TriageState>? state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TriageDetail?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TriageSummary?> GetByOriginReceiptAsync(Guid originReceiptId, CancellationToken cancellationToken) =>
            Task.FromResult(ByOriginReceipt.GetValueOrDefault(originReceiptId));
    }

    private sealed class FakeStatus : IQueuedIntakeStatusQueries, IIntakeReceiptQueries
    {
        private readonly Dictionary<IntakeSourceIdentity, QueuedIntakeStatus> statuses = [];

        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public void Add(Guid submissionId, QueuedIntakeStatusKind kind, Guid? processedReceiptId) =>
            statuses[PrincipalSubmissionPolicy.SourceIdentity(submissionId)] = new(
                Guid.NewGuid(),
                PrincipalInstructionPolicy.SourceFileName,
                Now,
                kind,
                processedReceiptId,
                FailureCode: null);

        public Task<QueuedIntakeStatus?> GetAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(statuses.Values.SingleOrDefault(status => status.StagedReceiptId == stagedReceiptId));

        public Task<QueuedIntakeStatus?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(statuses.GetValueOrDefault(sourceIdentity));

        Task<IntakeReceipt?> IIntakeReceiptQueries.GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipts.GetValueOrDefault(id));
    }
}
