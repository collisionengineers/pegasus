using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Case-filing replay's page against SQL Server: one statement for the
/// page and one grouped read of its plans, eligibility decided in SQL as
/// <see cref="CaseFilingTargetRule"/> decides it, the pipeline's associations
/// windowed from the cutoff Core passes in, and the replay history it records
/// (#1498, #1499; R189, R190, R377, R378).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseFilingReplayListingTests
{
    [Fact]
    public async Task TheGroupListingReturnsGroupMembersOnly()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = await harness.AddCaseAsync(CaseLifecycleState.NotReady);
        var older = new DateTimeOffset(2031, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var caseReceipt = await harness.AddReceiptAsync(caseId, ActorKind.Staff, older);
        var olderGroup = await harness.AddImageGroupAsync(older);
        var newerGroup = await harness.AddImageGroupAsync(older.AddHours(1));

        Assert.Equal(
            [olderGroup.ReceiptId, newerGroup.ReceiptId],
            await harness.GroupStore.ListPendingImageGroupReceiptsAsync(3));
        Assert.Equal([olderGroup.ReceiptId], await harness.GroupStore.ListPendingImageGroupReceiptsAsync(1));
        Assert.Contains(caseReceipt.ReceiptId, (await harness.ListAsync()).Select(item => item.ReceiptId));
    }

    /// <summary>
    /// A pipeline association to a Case it may no longer file on, or to a
    /// Triage Case, and any association to an archived Case, is neither listed
    /// nor evaluated one by one: the page is one statement however many there
    /// are (OO-1465-T5). With Engineer (ReportPreparation) is a state the
    /// pipeline files into, so it is listed.
    /// </summary>
    [Theory]
    [InlineData("report-sent", nameof(ActorKind.SystemWorker), nameof(CaseLifecycleState.ReportSent), false, false)]
    [InlineData("report-evidence", nameof(ActorKind.SystemWorker), nameof(CaseLifecycleState.NotReady), true, false)]
    [InlineData("cancelled", nameof(ActorKind.SystemWorker), nameof(CaseLifecycleState.PrincipalCancelled), false, false)]
    [InlineData("archived", nameof(ActorKind.Staff), nameof(CaseLifecycleState.Review), false, true)]
    [InlineData("system-to-triage", nameof(ActorKind.SystemWorker), null, false, false)]
    public async Task AnIneligibleTargetIsNeitherListedNorEvaluated(
        string row,
        string actor,
        string? state,
        bool reportSent,
        bool archived)
    {
        Assert.NotEmpty(row);
        await using var harness = await Harness.CreateAsync();
        var linkedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var index = 0; index < 12; index++)
        {
            var caseId = state is null
                ? await harness.AddTriageCaseAsync()
                : await harness.AddCaseAsync(Enum.Parse<CaseLifecycleState>(state), reportSent, archived);
            await harness.AddReceiptAsync(caseId, Enum.Parse<ActorKind>(actor), linkedAtUtc.AddSeconds(index));
        }
        var withEngineer = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.ReportPreparation),
            ActorKind.SystemWorker,
            linkedAtUtc);
        harness.Commands.Reset();

        var listed = await harness.ListAsync();

        Assert.Equal([withEngineer.ReceiptId], listed.Select(item => item.ReceiptId));
        // The page, then the one grouped plan read only when a listed pair has
        // a plan; this one has none.
        Assert.True(harness.Commands.Count == 1, harness.Commands.Describe());
    }

    [Fact]
    public async Task APageIsTwoStatementsWhateverTheBacklog()
    {
        await using var harness = await Harness.CreateAsync();
        var linkedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var index = 0; index < 15; index++)
        {
            await harness.AddReceiptAsync(
                await harness.AddCaseAsync(CaseLifecycleState.Review),
                ActorKind.SystemWorker,
                linkedAtUtc.AddSeconds(index),
                DocumentCustodyStatus.Pending);
        }
        harness.Commands.Reset();

        var page = await harness.ListAsync(10);

        Assert.Equal(10, page.Count);
        Assert.All(page, item =>
        {
            Assert.Equal(1, item.PlannedOccurrences);
            Assert.Equal(1, item.RecordedOccurrences);
            Assert.Equal(1, item.PendingOccurrences);
        });
        Assert.True(harness.Commands.Count == 2, harness.Commands.Describe());
    }

    /// <summary>
    /// R189 as read by R377: a pipeline association is listed for the
    /// existing two hours after it was made; a member of staff's is not
    /// windowed.
    /// </summary>
    [Fact]
    public async Task TheCutoffKeepsStaffAndDropsAnOldSystemAssociation()
    {
        await using var harness = await Harness.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var oldStaff = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.Review), ActorKind.Staff, now.AddHours(-3));
        var oldSystem = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.Review), ActorKind.SystemWorker, now.AddHours(-3));
        var recentSystem = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.Review), ActorKind.SystemWorker, now.AddMinutes(-90));

        var listed = (await harness.ListAsync()).Select(item => item.ReceiptId).ToArray();

        Assert.Contains(oldStaff.ReceiptId, listed);
        Assert.Contains(recentSystem.ReceiptId, listed);
        Assert.DoesNotContain(oldSystem.ReceiptId, listed);
    }

    /// <summary>
    /// The SQL predicate mirrors <see cref="CaseFilingTargetRule"/>: over every
    /// state, report sent or not, archived or not, a live lease or not, for
    /// both kinds of association, and over Triage Cases and origin receipts, a
    /// pair is listed exactly when the rule answers Current.
    /// </summary>
    [Fact]
    public async Task TheListingAgreesWithTheTargetRule()
    {
        await using var harness = await Harness.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var linkedAtUtc = now.AddMinutes(-10);
        var pairs = new List<(Guid CaseId, Guid ReceiptId)>();
        foreach (var actor in new[] { ActorKind.SystemWorker, ActorKind.Staff })
        {
            foreach (var state in Enum.GetValues<CaseLifecycleState>())
            {
                foreach (var reportSent in new[] { false, true })
                {
                    foreach (var archived in new[] { false, true })
                    {
                        foreach (var leased in new[] { false, true })
                        {
                            var caseId = await harness.AddCaseAsync(
                                state, reportSent, archived, leased ? now.AddHours(1) : now.AddMinutes(-1));
                            pairs.Add((caseId, (await harness.AddReceiptAsync(caseId, actor, linkedAtUtc)).ReceiptId));
                        }
                    }
                }
            }

            foreach (var hasTriage in new[] { false, true })
            {
                var caseId = await harness.AddTriageCaseAsync(hasTriage);
                pairs.Add((caseId, (await harness.AddReceiptAsync(caseId, actor, linkedAtUtc)).ReceiptId));
            }

            var origin = await harness.AddCaseAsync(CaseLifecycleState.Review);
            var originReceipt = await harness.AddReceiptAsync(origin, actor, linkedAtUtc);
            await harness.MakeOriginAsync(origin, originReceipt.ReceiptId);
            pairs.Add((origin, originReceipt.ReceiptId));
        }

        var listed = (await harness.ListAsync(pairs.Count + 1)).Select(item => item.ReceiptId).ToHashSet();
        var current = new HashSet<Guid>();
        await using (var db = await harness.Database.CreateContextAsync())
        {
            foreach (var (caseId, receiptId) in pairs)
            {
                var target = await IntakePromotionTarget.EvaluateAsync(db, caseId, receiptId, now, default);
                if (target.Disposition == CaseFilingTargetDisposition.Current)
                {
                    current.Add(receiptId);
                }
            }
        }

        Assert.NotEmpty(current);
        Assert.True(current.Count < pairs.Count);
        Assert.Equal(current.Order(), listed.Order());
    }

    /// <summary>
    /// R190: a non-transient fault records <c>replay_exhausted</c> at once and
    /// the receipt's work item stays completed; the next sweep does not list
    /// or attempt it.
    /// </summary>
    [Fact]
    public async Task ANonTransientReplayFailureIsNotRetriedOnTheNextSweep()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.NotReady),
            ActorKind.Staff,
            new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero));
        var promotion = new ScriptedPromotionStore(new InvalidOperationException("The promotion plan cannot be read."));
        var sweep = harness.Sweep(promotion);

        var first = await sweep.ExecuteAsync(20);

        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 0, 1, 1), first);
        Assert.Equal(1, promotion.Calls);
        await using (var verify = await harness.Database.CreateContextAsync())
        {
            var work = await verify.IntakeWorkItems.SingleAsync(item => item.StagedReceiptId == seeded.StagedReceiptId);
            Assert.Equal("completed", work.State);
            var exhausted = await verify.IntakeMutationHistory.SingleAsync(item =>
                item.IntakeReceiptId == seeded.ReceiptId && item.EventType == "replay_exhausted");
            Assert.Equal("unexpected_intake_processing_failure", exhausted.Reason);
            Assert.Equal(seeded.CaseId, exhausted.CaseId);
        }

        Assert.DoesNotContain(seeded.ReceiptId, (await harness.ListAsync()).Select(item => item.ReceiptId));
        var second = await sweep.ExecuteAsync(20);
        Assert.Equal(0, second.Candidates);
        Assert.Equal(1, promotion.Calls);
    }

    /// <summary>
    /// A transient fault records <c>replay_failed</c>; the page counts those
    /// rows for the current association only, and a recorded key counts as
    /// recorded.
    /// </summary>
    [Fact]
    public async Task AFailedAttemptIsCountedForTheCurrentLinkOnly()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.Review),
            ActorKind.Staff,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var sweep = harness.Sweep(new ScriptedPromotionStore(new TimeoutException("The filing connection timed out.")));

        var first = await sweep.ExecuteAsync(20);

        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 0, 1, 0), first);
        var listed = Assert.Single(await harness.ListAsync());
        Assert.Equal(1, listed.FailedAttempts);
        Assert.NotNull(listed.LastFailedAtUtc);
        await harness.Store.RecordAsync(
            new(listed with { FailedAttempts = 0 }, CaseFilingReplayEvent.Failed, 1, "intake_processing_failure",
                DateTimeOffset.UtcNow),
            default);
        Assert.Equal(1, Assert.Single(await harness.ListAsync()).FailedAttempts);
        // The 30-second delay has not passed, so the next sweep does not try.
        Assert.Equal(0, (await sweep.ExecuteAsync(20)).Candidates);

        await harness.RelinkAsync(seeded.ReceiptId, DateTimeOffset.UtcNow.AddSeconds(1));

        var relinked = Assert.Single(await harness.ListAsync());
        Assert.Equal(0, relinked.FailedAttempts);
        Assert.Null(relinked.LastFailedAtUtc);
    }

    /// <summary>
    /// The listing names only a source the filer selects, so a listed pair
    /// is never one the filer answers has nothing to file, which would leave
    /// no row and be listed again every tick.
    /// </summary>
    [Theory]
    [InlineData("short-hash")]
    [InlineData("blank-file-name")]
    [InlineData("blank-media-type")]
    [InlineData("blank-storage-key")]
    public async Task ASourceTheFilerWouldNotSelectIsNotListed(string row)
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.Review),
            ActorKind.Staff,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Single(await harness.ListAsync());
        await using (var context = await harness.Database.CreateContextAsync())
        {
            var source = context.IntakeAssets.Where(item => item.IntakeReceiptId == seeded.ReceiptId);
            _ = row switch
            {
                "short-hash" => await source.ExecuteUpdateAsync(update =>
                    update.SetProperty(item => item.ContentHash, "d")),
                "blank-file-name" => await source.ExecuteUpdateAsync(update =>
                    update.SetProperty(item => item.FileName, " ")),
                "blank-media-type" => await source.ExecuteUpdateAsync(update =>
                    update.SetProperty(item => item.MediaType, " ")),
                _ => await source.ExecuteUpdateAsync(update =>
                    update.SetProperty(item => item.StorageKey, " "))
            };
        }

        Assert.Empty(await harness.ListAsync());
    }

    [Theory]
    [InlineData("pending", true, false)]
    [InlineData("unrecorded", false, true)]
    public async Task APlannedOccurrenceSetIsReplayedUnlessOneIsPending(string row, bool recordPending, bool replayed)
    {
        Assert.NotEmpty(row);
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddReceiptAsync(
            await harness.AddCaseAsync(CaseLifecycleState.NotReady),
            ActorKind.Staff,
            new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero),
            DocumentCustodyStatus.Pending,
            recordOccurrence: recordPending);
        var promotion = new ScriptedPromotionStore(null);

        var listed = Assert.Single(await harness.ListAsync());
        await harness.Sweep(promotion).ExecuteAsync(20);

        Assert.Equal(seeded.ReceiptId, listed.ReceiptId);
        Assert.Equal(1, listed.PlannedOccurrences);
        Assert.Equal(recordPending ? 1 : 0, listed.RecordedOccurrences);
        Assert.Equal(replayed ? 1 : 0, promotion.Calls);
    }

    private sealed record SeededReceipt(Guid ReceiptId, Guid StagedReceiptId, Guid CaseId);

    /// <summary>The filer's plan step, failing with the given fault or answering that nothing is left to file.</summary>
    private sealed class ScriptedPromotionStore(Exception? fault) : IAutomaticCaseEvidencePromotionStore
    {
        public int Calls { get; private set; }

        public Task<AutomaticCaseEvidencePromotionPreparation> PrepareAsync(
            AutomaticCaseEvidencePromotionRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return fault is null
                ? Task.FromResult(new AutomaticCaseEvidencePromotionPreparation(
                    AutomaticCaseEvidencePromotionPreparationDisposition.NotApplicable))
                : Task.FromException<AutomaticCaseEvidencePromotionPreparation>(fault);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly Guid principalId = Guid.NewGuid();
        private readonly Guid lineageId = Guid.NewGuid();
        private int nextSequence = 1;
        private int nextDocumentOrdinal = 1;

        private Harness(
            LocalDbTestDatabase database,
            IDbContextFactory<PegasusDbContext> factory,
            CommandCountingInterceptor commands)
        {
            Database = database;
            Factory = factory;
            Commands = commands;
            Store = new EfCaseFilingReplayStore(factory);
            GroupStore = new EfIntakeSubmissionGroupStore(factory);
        }

        public LocalDbTestDatabase Database { get; }
        public IDbContextFactory<PegasusDbContext> Factory { get; }
        public EfCaseFilingReplayStore Store { get; }
        public EfIntakeSubmissionGroupStore GroupStore { get; }
        public CommandCountingInterceptor Commands { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var commands = new CommandCountingInterceptor();
            var options = new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString)
                .AddInterceptors(commands);
            var harness = new Harness(database, new PooledDbContextFactory<PegasusDbContext>(options.Options), commands);
            await harness.SeedPrincipalAsync();
            return harness;
        }

        public Task<IReadOnlyList<CaseFilingReplayCandidate>> ListAsync(int maximumItems = 50) =>
            Store.ListDueAsync(
                null,
                IntakeEscapeWindow.LatestClosedStart(DateTimeOffset.UtcNow),
                DateTimeOffset.UtcNow,
                maximumItems,
                default);

        public ReplayCaseFilings Sweep(IAutomaticCaseEvidencePromotionStore promotion) =>
            new(
                Store,
                new EfIntakeReceiptStore(Factory),
                new PromoteAssociatedIntakeCaseEvidence(null!, null!, promotion),
                TimeProvider.System);

        public async Task<Guid> AddCaseAsync(
            CaseLifecycleState state,
            bool reportSent = false,
            bool archived = false,
            DateTimeOffset? editLeaseExpiresAtUtc = null)
        {
            await using var context = await Database.CreateContextAsync();
            if (reportSent)
            {
                // Report-sent evidence is a full send record; the listing
                // reads only that the workflow names one.
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE [CaseWorkflows] NOCHECK CONSTRAINT ALL");
            }

            var caseId = AddCase(context, "audit");
            var leased = editLeaseExpiresAtUtc is not null;
            context.CaseWorkflows.Add(new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = state.ToString(),
                Version = 1,
                ConcurrencyToken = Guid.NewGuid(),
                ReportSentEvidenceId = reportSent ? Guid.NewGuid() : null,
                ArchivedAtUtc = archived ? DateTimeOffset.UtcNow.AddDays(-1) : null,
                ArchivedByKind = archived ? nameof(ActorKind.Staff) : null,
                ArchivedBySubjectId = archived ? "staff:test" : null,
                ArchivedByRolesJson = archived ? "[]" : null,
                ArchiveReason = archived ? "test" : null,
                EditLeaseHolder = leased ? "staff:test" : null,
                EditLeaseHolderKind = leased ? nameof(ActorKind.Staff) : null,
                EditLeaseTokenHash = leased ? new string('A', 64) : null,
                EditLeaseExpiresAtUtc = editLeaseExpiresAtUtc
            });
            await context.SaveChangesAsync();
            return caseId;
        }

        public async Task<Guid> AddTriageCaseAsync(bool hasTriage = true)
        {
            await using var context = await Database.CreateContextAsync();
            var caseId = AddCase(context, "triage");
            if (hasTriage)
            {
                context.Triage.Add(new TriageEntity
                {
                    CaseId = caseId,
                    NormalizedVehicleRegistration = "AB12CDE",
                    State = "open",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreationOperationKey = $"triage:{caseId:N}",
                    Version = 0,
                    ConcurrencyToken = Guid.NewGuid()
                });
            }

            await context.SaveChangesAsync();
            return caseId;
        }

        public async Task MakeOriginAsync(Guid caseId, Guid receiptId)
        {
            await using var context = await Database.CreateContextAsync();
            await context.Cases.Where(item => item.Id == caseId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.OriginIntakeReceiptId, (Guid?)receiptId));
        }

        public async Task<SeededReceipt> AddReceiptAsync(
            Guid caseId,
            ActorKind actor,
            DateTimeOffset linkedAtUtc,
            DocumentCustodyStatus? plannedCustody = null,
            bool recordOccurrence = true)
        {
            var token = Guid.NewGuid().ToString("N");
            var stagedReceiptId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var hash = new string('d', 64);
            await using var context = await Database.CreateContextAsync();
            context.AddRange(
                ImageStagedReceipt(stagedReceiptId, token, hash, linkedAtUtc),
                ImageReceipt(receiptId, token, hash, linkedAtUtc),
                ImageSourceAsset(receiptId, hash, assetId),
                new IntakeManualAssociationEntity
                {
                    IntakeReceiptId = receiptId, CaseId = caseId, IsActive = true, Version = 1,
                    LinkedAtUtc = linkedAtUtc, ActorKind = actor.ToString(), ActorSubjectId = "test",
                    ActorRolesJson = "[]", LastOperationKey = $"link:{token}"
                },
                CompletedWork(stagedReceiptId, receiptId, $"case:{token}", linkedAtUtc),
                Evaluation(stagedReceiptId, receiptId, linkedAtUtc));
            if (plannedCustody is { } custody)
            {
                context.Add(new IntakeMutationHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    IntakeReceiptId = receiptId,
                    CaseId = caseId,
                    EventType = "intake_case_evidence_promotion_started",
                    ActorKind = "SystemWorker",
                    ActorSubjectId = "intake-processing",
                    ActorRolesJson = "[]",
                    Reason = "seed",
                    OperationKey = AutomaticCaseEvidencePromotionOperationKey.Plan(receiptId),
                    RequestFingerprint = new string('e', 64),
                    OccurredAtUtc = linkedAtUtc,
                    AfterJson = JsonSerializer.Serialize(new AutomaticCaseEvidencePromotionPlan([assetId], true))
                });
                if (recordOccurrence)
                {
                    var documentId = Guid.NewGuid();
                    var versionId = Guid.NewGuid();
                    var ordinal = nextDocumentOrdinal++;
                    context.AddRange(
                        new CaseDocumentEntity
                        {
                            Id = documentId, CaseId = caseId, Ordinal = ordinal,
                            SourceOccurrenceIdentity = assetId.ToString("N")
                        },
                        new DocumentVersionEntity
                        {
                            Id = versionId, DocumentId = documentId, Version = 1, FileName = "case.jpg",
                            MediaType = "image/jpeg", ContentLength = 3, Sha256 = hash, CustodyStatus = custody,
                            CreatedAtUtc = linkedAtUtc, CreatedBy = "SystemWorker:intake-processing", IsCurrent = true
                        },
                        new DocumentOccurrenceEntity
                        {
                            Id = Guid.NewGuid(), CaseId = caseId, DocumentId = documentId, VersionId = versionId,
                            Ordinal = ordinal, SemanticRole = DocumentSemanticRole.Image, Source = DocumentSource.Intake,
                            SourceOccurrenceIdentity = assetId.ToString("N"), RecordedAtUtc = linkedAtUtc,
                            OperationKey = AutomaticCaseEvidencePromotionOperationKey.For(caseId, receiptId, assetId)
                        });
                }
            }

            await context.SaveChangesAsync();
            return new(receiptId, stagedReceiptId, caseId);
        }

        /// <summary>An unlink and a later link to the same Case: a new association version and link time.</summary>
        public async Task RelinkAsync(Guid receiptId, DateTimeOffset linkedAtUtc, ActorKind actor = ActorKind.Staff)
        {
            await using var context = await Database.CreateContextAsync();
            await context.IntakeManualAssociations.Where(item => item.IntakeReceiptId == receiptId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Version, item => item.Version + 2)
                    .SetProperty(item => item.LinkedAtUtc, linkedAtUtc)
                    .SetProperty(item => item.ActorKind, actor.ToString()));
        }

        public async Task<SeededReceipt> AddImageGroupAsync(DateTimeOffset receivedAtUtc)
        {
            var token = Guid.NewGuid().ToString("N");
            var stagedReceiptId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var hash = new string('c', 64);
            await using var context = await Database.CreateContextAsync();
            context.AddRange(
                ImageStagedReceipt(stagedReceiptId, token, hash, receivedAtUtc),
                ImageReceipt(receiptId, token, hash, receivedAtUtc),
                ImageSourceAsset(receiptId, hash),
                new IntakeSubmissionGroupEntity
                {
                    Id = groupId, SourceChannel = "manual_upload", SubmissionToken = token,
                    ExpectedMemberCount = 2, Actor = "staff:test", ReceivedAtUtc = receivedAtUtc, Version = 1
                },
                new IntakeSubmissionGroupMemberEntity
                {
                    Id = Guid.NewGuid(), GroupId = groupId, Ordinal = 0, StagedReceiptId = stagedReceiptId,
                    SourceFileName = "group.jpg", SourceHash = hash, AddedAtUtc = receivedAtUtc
                },
                CompletedWork(stagedReceiptId, receiptId, $"group:{token}", receivedAtUtc),
                Evaluation(stagedReceiptId, receiptId, receivedAtUtc));
            await context.SaveChangesAsync();
            return new(receiptId, stagedReceiptId, Guid.Empty);
        }

        private Guid AddCase(PegasusDbContext context, string type)
        {
            var caseId = Guid.NewGuid();
            var sequence = nextSequence++;
            context.Cases.Add(new CaseEntity
            {
                Id = caseId, PrincipalId = principalId, SequenceLineageId = lineageId,
                Year = 2031, Sequence = sequence, Reference = $"REPL31{sequence:000}", Type = type,
                InitialState = type == "triage" ? null : "NotReady", CustodyState = "Pending",
                CreatedAtUtc = DateTimeOffset.UtcNow, Version = 1, ConcurrencyToken = Guid.NewGuid()
            });
            return caseId;
        }

        private async Task SeedPrincipalAsync()
        {
            await using var context = await Database.CreateContextAsync();
            var organizationId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Role = "principal", Id = organizationId, Name = "Case filing replay test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = DateTimeOffset.UtcNow },
                new PrincipalEntity
                {
                    Id = principalId, OrganizationId = organizationId, SequenceLineageId = lineageId,
                    Code = "REPL", IsActive = true, Version = 0
                });
            await context.SaveChangesAsync();
        }

        private static IntakeStagedReceiptEntity ImageStagedReceipt(
            Guid id, string token, string hash, DateTimeOffset receivedAtUtc) => new()
        {
            Id = id, SourceFileName = "case.jpg", MediaType = "image/jpeg", SourceLength = 3, SourceHash = hash,
            SourceChannel = "manual_upload", ExternalReceiptToken = token, ReceivedAtUtc = receivedAtUtc,
            Actor = "staff:test", StorageKey = $"manual/{token}", StagedAtUtc = receivedAtUtc
        };

        private static IntakeReceiptEntity ImageReceipt(
            Guid id, string token, string hash, DateTimeOffset receivedAtUtc) => new()
        {
            Id = id, SourceFileName = "case.jpg", MediaType = "image/jpeg", SourceLength = 3, SourceHash = hash,
            SourceChannel = "manual_upload", ExternalReceiptToken = token, ReceivedAtUtc = receivedAtUtc,
            ProcessedAtUtc = receivedAtUtc, SourceReaderKey = "test", SourceReaderVersion = "1", Version = 1,
            Decision = "needs_sorting", DecisionReason = "test",
            EvidenceJson = EfIntakeReceiptStore.SerializeEvidence([]),
            FieldsJson = EfIntakeReceiptStore.SerializeFields([]),
            OcrCandidatesJson = EfIntakeReceiptStore.SerializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>([])
        };

        private static IntakeAssetEntity ImageSourceAsset(Guid receiptId, string hash, Guid? assetId = null) => new()
        {
            Id = assetId ?? Guid.NewGuid(), IntakeReceiptId = receiptId, SourceLabel = "uploaded source",
            FileName = "case.jpg", MediaType = "image/jpeg", Kind = "source", Disposition = "source",
            ContentLength = 3, ContentHash = hash, StorageKey = $"retained/{receiptId:N}"
        };

        private static IntakeWorkItemEntity CompletedWork(
            Guid stagedReceiptId, Guid receiptId, string operationKey, DateTimeOffset completedAtUtc) => new()
        {
            Id = Guid.NewGuid(), StagedReceiptId = stagedReceiptId, OperationKey = operationKey,
            State = "completed", AttemptCount = 1, DueAtUtc = completedAtUtc,
            ProcessedReceiptId = receiptId, CompletedAtUtc = completedAtUtc
        };

        private static IntakeEvaluationEntity Evaluation(
            Guid stagedReceiptId, Guid receiptId, DateTimeOffset evaluatedAtUtc) => new()
        {
            Id = Guid.NewGuid(), StagedReceiptId = stagedReceiptId, ProcessedReceiptId = receiptId,
            Revision = 1, EvaluatedAtUtc = evaluatedAtUtc
        };

        public async ValueTask DisposeAsync() => await Database.DisposeAsync();
    }
}
