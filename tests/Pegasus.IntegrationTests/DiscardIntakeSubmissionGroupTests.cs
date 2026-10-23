using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>Durable terminal-discard safeguards, exercised against SQL Server.</summary>
[Trait("Category", "SqlServer")]
public sealed class DiscardIntakeSubmissionGroupTests
{
    [Fact]
    public async Task UnlinkedCompletedGroupDiscardsOnceReplaysAndRetainsTheSource()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.DiscardAsync();
        var replay = await harness.DiscardAsync();

        Assert.True(result.Succeeded);
        Assert.False(result.IsReplay);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);

        await using var context = await harness.Database.CreateContextAsync();
        var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
        Assert.NotNull(group.DiscardedAtUtc);
        Assert.Equal(harness.Actor.SubjectId, group.DiscardedByActorSubjectId);
        Assert.Equal(2, group.Version);
        Assert.Single(await context.IntakeSubmissionGroupHistory.ToListAsync());
        Assert.Single(await context.IntakeStagedReceipts.Where(item => item.Id == harness.StagedReceiptId).ToListAsync());
        Assert.Single(await context.IntakeReceipts.Where(item => item.Id == harness.ReceiptId).ToListAsync());
    }

    [Fact]
    public async Task StaleGroupVersionAndChangedMembershipAreRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var stale = await harness.DiscardAsync(expectedGroupVersion: 0);
        Assert.False(stale.Succeeded);

        await using (var context = await harness.Database.CreateContextAsync())
        {
            var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
            var stagedReceiptId = Guid.NewGuid();
            context.IntakeStagedReceipts.Add(new()
            {
                Id = stagedReceiptId, SourceFileName = "later-member.jpg", MediaType = "image/jpeg",
                SourceLength = 3, SourceHash = new string('b', 64), SourceChannel = "manual_upload",
                ExternalReceiptToken = "discard-test:1", ReceivedAtUtc = DateTimeOffset.UtcNow,
                Actor = "staff:test", StorageKey = "manual/later-member", StagedAtUtc = DateTimeOffset.UtcNow
            });
            group.Version++;
            group.ExpectedMemberCount = 2;
            context.IntakeSubmissionGroupMembers.Add(new()
            {
                Id = Guid.NewGuid(), GroupId = group.Id, Ordinal = 1,
                StagedReceiptId = stagedReceiptId, SourceFileName = "later-member.jpg",
                SourceHash = new string('b', 64), AddedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var changed = await harness.DiscardAsync();
        Assert.False(changed.Succeeded);
        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task ActiveManualAssociationRefusesDiscard()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddActiveAssociationAsync();

        var result = await harness.DiscardAsync();

        Assert.False(result.Succeeded);
        await harness.AssertActionableAsync();
    }
    [Theory]
    [InlineData("allocation")]
    [InlineData("image")]
    public async Task AllocationOrImageRegistrationRefusesDiscard(string existingOutcome)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddExistingOutcomeAsync(existingOutcome);

        var result = await harness.DiscardAsync();

        Assert.False(result.Succeeded);
        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task UnauthorizedActorCannotReachTheSqlStore()
    {
        await using var harness = await Harness.CreateAsync();
        var request = harness.Request(ActionActor.SystemWorker("test-worker"));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new DiscardIntakeSubmissionGroup(harness.Store, TimeProvider.System).ExecuteAsync(request));

        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task ProcessingRaceHasOneValidOutcomeAndNeverCommitsAnInvalidDiscard()
    {
        await using var harness = await Harness.CreateAsync();
        var processing = Task.Run(async () =>
        {
            await using var context = await harness.Database.CreateContextAsync();
            var work = await context.IntakeWorkItems.SingleAsync(item => item.StagedReceiptId == harness.StagedReceiptId);
            work.State = "processing";
            await context.SaveChangesAsync();
        });
        var discard = harness.DiscardAsync();
        await processing;
        var result = await discard;

        await using var verify = await harness.Database.CreateContextAsync();
        var group = await verify.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
        if (result.Succeeded)
        {
            Assert.NotNull(group.DiscardedAtUtc);
        }
        else
        {
            Assert.Null(group.DiscardedAtUtc);
        }
    }

    [Fact]
    public async Task TransactionFailureRollsBackTheTerminalDecision()
    {
        var interceptor = new FailDiscardSaveInterceptor();
        await using var harness = await Harness.CreateAsync(interceptor);

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.DiscardAsync());

        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task PendingImageSweepListSkipsReceiptsAlreadyLinkedToACase()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddActiveAssociationAsync();

        var pending = await harness.Store.ListPendingImageGroupReceiptsAsync(50);

        Assert.DoesNotContain(harness.ReceiptId, pending);
    }

    [Fact]
    public async Task AFullGroupPageStillListsUnpromotedCaseReceiptsOldestFirst()
    {
        await using var harness = await Harness.CreateAsync();
        var older = new DateTimeOffset(2031, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var newer = older.AddHours(2);
        var olderReceipt = await harness.AddUnpromotedCaseReceiptAsync(older);
        var newerReceipt = await harness.AddUnpromotedCaseReceiptAsync(newer);
        var olderGroup = await harness.AddImageGroupAsync(older);
        var newerGroup = await harness.AddImageGroupAsync(newer.AddHours(1));

        Assert.Equal(
            [olderReceipt.ReceiptId],
            await harness.Store.ListPendingImageGroupReceiptsAsync(1));
        // Two unresolved groups would fill a page of 2 on their own. The
        // unpromoted filings still take the page, oldest association first.
        Assert.Equal(
            [olderReceipt.ReceiptId, newerReceipt.ReceiptId],
            await harness.Store.ListPendingImageGroupReceiptsAsync(2));
        Assert.Equal(
            [olderReceipt.ReceiptId, newerReceipt.ReceiptId, olderGroup.ReceiptId],
            await harness.Store.ListPendingImageGroupReceiptsAsync(3));
        Assert.DoesNotContain(
            newerGroup.ReceiptId,
            await harness.Store.ListPendingImageGroupReceiptsAsync(3));
    }
    [Fact]
    public async Task TheGroupedImageListStopsAtTheLimitAfterOnePage()
    {
        var statements = new SqlStatementCounter();
        await using var harness = await Harness.CreateAsync(statements);
        var first = new DateTimeOffset(2031, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var groups = new List<SeededReceipt>();
        for (var index = 0; index < 4; index++)
        {
            groups.Add(await harness.AddImageGroupAsync(first.AddHours(index)));
        }

        statements.Reset();
        Assert.Equal(
            [groups[0].ReceiptId],
            await harness.Store.ListPendingImageGroupReceiptsAsync(1));
        Assert.Equal(1, GroupPageStatements(statements));

        statements.Reset();
        Assert.Equal(
            groups.Take(3).Select(group => group.ReceiptId),
            await harness.Store.ListPendingImageGroupReceiptsAsync(3));
        Assert.Equal(1, GroupPageStatements(statements));

        statements.Reset();
        Assert.Equal(
            groups.Select(group => group.ReceiptId),
            await harness.Store.ListPendingImageGroupReceiptsAsync(10));
        Assert.Equal(1, GroupPageStatements(statements));
        Assert.Equal(0, statements.CountContaining("OFFSET"));
    }

    [Fact]
    public async Task TheGroupedImageListPagesPastIneligibleRowsAndSpendsAGroupSplitAcrossPages()
    {
        var statements = new SqlStatementCounter();
        await using var harness = await Harness.CreateAsync(statements);
        var first = new DateTimeOffset(2031, 5, 1, 8, 0, 0, TimeSpan.Zero);
        const string Photo = "image/jpeg";
        const string Archive = "application/zip";
        // Rows in order, three to a page:
        //   page 1: g1/0 (ineligible), g1/1 (eligible), g2/0 (eligible, spends g2)
        //   page 2: g2/1 (eligible but g2 is spent: the group continues from page 1),
        //           g3/0 and g3/1 (both ineligible)
        //   page 3: g4/0 (eligible)
        var g1 = await harness.AddImageGroupMembersAsync(first, Archive, Photo);
        var g2 = await harness.AddImageGroupMembersAsync(first.AddHours(1), Photo, Photo);
        await harness.AddImageGroupMembersAsync(first.AddHours(2), Archive, Archive);
        var g4 = await harness.AddImageGroupMembersAsync(first.AddHours(3), Photo);
        Guid[] expected = [g1[1].ReceiptId, g2[0].ReceiptId, g4[0].ReceiptId];

        // The limit is reached on the third page.
        statements.Reset();
        Assert.Equal(expected, await harness.Store.ListPendingImageGroupReceiptsAsync(3));
        Assert.Equal(3, GroupPageStatements(statements));

        // The last page is short, so the read ends there with fewer than the limit.
        statements.Reset();
        Assert.Equal(expected, await harness.Store.ListPendingImageGroupReceiptsAsync(4));
        Assert.Equal(2, GroupPageStatements(statements));
        Assert.Equal(0, statements.CountContaining("OFFSET"));
    }

    [Fact]
    public async Task ANonTransientReplayFailureIsNotRetriedOnTheNextSweep()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddUnpromotedCaseReceiptAsync(
            new DateTimeOffset(2031, 5, 2, 9, 0, 0, TimeSpan.Zero));
        var processor = new ThrowingProcessor(new InvalidOperationException("The promotion plan cannot be read."));
        var sweep = harness.Sweep(seeded.ReceiptId, processor);

        var first = await sweep.ExecuteAsync(20);

        Assert.Equal(1, first.Candidates);
        Assert.Equal(1, first.Failures);
        Assert.Equal(0, first.Escaped);
        Assert.Equal(1, processor.Calls);
        await using (var verify = await harness.Database.CreateContextAsync())
        {
            var work = await verify.IntakeWorkItems.SingleAsync(item => item.StagedReceiptId == seeded.StagedReceiptId);
            Assert.Equal("failed", work.State);
            Assert.Equal("unexpected_intake_processing_failure", work.FailureCode);
        }

        Assert.DoesNotContain(seeded.ReceiptId, await harness.Store.ListPendingImageGroupReceiptsAsync(20));
        var second = await sweep.ExecuteAsync(20);
        Assert.Equal(0, second.Candidates);
        Assert.Equal(1, processor.Calls);
    }

    [Theory]
    [InlineData(nameof(DocumentCustodyStatus.Failed), true)]
    [InlineData(nameof(DocumentCustodyStatus.Pending), false)]
    public async Task APlannedOccurrenceSetIsReplayedUnlessOneIsPending(string custody, bool replayed)
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.AddUnpromotedCaseReceiptAsync(
            new DateTimeOffset(2031, 5, 3, 9, 0, 0, TimeSpan.Zero),
            Enum.Parse<DocumentCustodyStatus>(custody));
        var processor = new CountingProcessor();

        var pending = await harness.Store.ListPendingImageGroupReceiptsAsync(20);
        await harness.Sweep(seeded.ReceiptId, processor).ExecuteAsync(20);

        if (replayed)
        {
            Assert.Contains(seeded.ReceiptId, pending);
            Assert.Equal(1, processor.Calls);
        }
        else
        {
            Assert.DoesNotContain(seeded.ReceiptId, pending);
            Assert.Equal(0, processor.Calls);
        }
    }

    /// <summary>The grouped-image page reads: the only statements that join the group members.</summary>
    private static int GroupPageStatements(SqlStatementCounter statements) =>
        statements.CountContaining("JOIN [IntakeSubmissionGroupMembers]");

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(LocalDbTestDatabase database, IDbContextFactory<PegasusDbContext> factory)
        {
            Database = database;
            Factory = factory;
            Store = new EfIntakeSubmissionGroupStore(factory);
        }

        public LocalDbTestDatabase Database { get; }
        public IDbContextFactory<PegasusDbContext> Factory { get; }
        public EfIntakeSubmissionGroupStore Store { get; }
        public Guid GroupId { get; } = Guid.NewGuid();
        public Guid StagedReceiptId { get; } = Guid.NewGuid();
        public Guid ReceiptId { get; } = Guid.NewGuid();
        public Guid OperationId { get; } = Guid.NewGuid();
        public ActionActor Actor { get; } = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        public Guid CaseId { get; private set; }
        private int nextDocumentOrdinal = 1;
        private int nextCaseSequence = 1;

        public static async Task<Harness> CreateAsync(IInterceptor? interceptor = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var options = new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
            var harness = new Harness(database, new PooledDbContextFactory<PegasusDbContext>(options.Options));
            await harness.SeedAsync();
            return harness;
        }

        public DiscardIntakeSubmissionGroupRequest Request(ActionActor? actor = null, long? expectedGroupVersion = null) =>
            new(GroupId, expectedGroupVersion ?? 1, new Dictionary<Guid, long> { [ReceiptId] = 4 }, actor ?? Actor, OperationId);

        public Task<DiscardIntakeSubmissionGroupResult> DiscardAsync(long? expectedGroupVersion = null) =>
            new DiscardIntakeSubmissionGroup(Store, TimeProvider.System).ExecuteAsync(Request(expectedGroupVersion: expectedGroupVersion));

        public async Task AddActiveAssociationAsync()
        {
            await using var context = await Database.CreateContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Role = "principal", Id = organizationId, Name = "Discard association test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = DateTimeOffset.UtcNow },
                new PrincipalEntity
                {
                    Id = principalId, OrganizationId = organizationId, SequenceLineageId = lineageId,
                    Code = "DISC", IsActive = true, Version = 0
                },
                new CaseEntity
                {
                    Id = caseId, PrincipalId = principalId, SequenceLineageId = lineageId,
                    Year = 2031, Sequence = 1, Reference = "DISC31001", Type = "audit",
                    InitialState = "NotReady", CustodyState = "Pending", CreatedAtUtc = DateTimeOffset.UtcNow,
                    Version = 1, ConcurrencyToken = Guid.NewGuid()
                },
                new IntakeManualAssociationEntity
                {
                    IntakeReceiptId = ReceiptId, CaseId = caseId, IsActive = true, Version = 1,
                    LinkedAtUtc = DateTimeOffset.UtcNow, ActorKind = "Staff", ActorSubjectId = Actor.SubjectId,
                    ActorRolesJson = "[\"Engineer\"]", LastOperationKey = $"link:{Guid.NewGuid():N}"
                });
            await context.SaveChangesAsync();
        }

        public async Task<SeededReceipt> AddImageGroupAsync(DateTimeOffset receivedAtUtc) =>
            (await AddImageGroupMembersAsync(receivedAtUtc, "image/jpeg"))[0];

        /// <summary>
        /// One waiting group with one member per media type, in ordinal order.
        /// Every member passes the SQL query; a member whose media type is not
        /// an image, PDF or e-mail fails Core's eligibility rule.
        /// </summary>
        public async Task<IReadOnlyList<SeededReceipt>> AddImageGroupMembersAsync(
            DateTimeOffset receivedAtUtc,
            params string[] memberMediaTypes)
        {
            var groupToken = Guid.NewGuid().ToString("N");
            var groupId = Guid.NewGuid();
            var hash = new string('c', 64);
            var seeded = new List<SeededReceipt>();
            await using var context = await Database.CreateContextAsync();
            context.Add(new IntakeSubmissionGroupEntity
            {
                Id = groupId, SourceChannel = "manual_upload", SubmissionToken = groupToken,
                ExpectedMemberCount = 2, Actor = "staff:test", ReceivedAtUtc = receivedAtUtc, Version = 1
            });
            for (var ordinal = 0; ordinal < memberMediaTypes.Length; ordinal++)
            {
                var token = $"{groupToken}-{ordinal}";
                var stagedReceiptId = Guid.NewGuid();
                var receiptId = Guid.NewGuid();
                context.AddRange(
                    ImageStagedReceipt(stagedReceiptId, token, hash, receivedAtUtc),
                    ImageReceipt(receiptId, token, hash, receivedAtUtc, memberMediaTypes[ordinal]),
                    ImageSourceAsset(receiptId, hash),
                    new IntakeSubmissionGroupMemberEntity
                    {
                        Id = Guid.NewGuid(), GroupId = groupId, Ordinal = ordinal, StagedReceiptId = stagedReceiptId,
                        SourceFileName = "group.jpg", SourceHash = hash, AddedAtUtc = receivedAtUtc
                    },
                    CompletedWork(stagedReceiptId, receiptId, $"group:{token}", receivedAtUtc),
                    Evaluation(stagedReceiptId, receiptId, receivedAtUtc));
                seeded.Add(new(receiptId, stagedReceiptId));
            }

            await context.SaveChangesAsync();
            return seeded;
        }

        public async Task<SeededReceipt> AddUnpromotedCaseReceiptAsync(
            DateTimeOffset linkedAtUtc,
            DocumentCustodyStatus? plannedCustody = null)
        {
            var token = Guid.NewGuid().ToString("N");
            var stagedReceiptId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var hash = new string('d', 64);
            await using var context = await Database.CreateContextAsync();
            var caseId = await EnsureCaseAsync(context);
            context.AddRange(
                ImageStagedReceipt(stagedReceiptId, token, hash, linkedAtUtc),
                ImageReceipt(receiptId, token, hash, linkedAtUtc),
                ImageSourceAsset(receiptId, hash, assetId),
                new IntakeManualAssociationEntity
                {
                    IntakeReceiptId = receiptId, CaseId = caseId, IsActive = true, Version = 1,
                    LinkedAtUtc = linkedAtUtc, ActorKind = "Staff", ActorSubjectId = Actor.SubjectId,
                    ActorRolesJson = "[\"Engineer\"]", LastOperationKey = $"link:{token}"
                },
                CompletedWork(stagedReceiptId, receiptId, $"case:{token}", linkedAtUtc),
                Evaluation(stagedReceiptId, receiptId, linkedAtUtc));
            if (plannedCustody is { } custody)
            {
                var documentId = Guid.NewGuid();
                var versionId = Guid.NewGuid();
                var ordinal = nextDocumentOrdinal++;
                context.AddRange(
                    new IntakeMutationHistoryEntity
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
                    },
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

            await context.SaveChangesAsync();
            return new(receiptId, stagedReceiptId);
        }

        public ReconcileGroupedImageIntake Sweep(Guid receiptId, IProcessQueuedIntake processor) =>
            new(
                new FixedReceiptQueries(AssociatedReceipt(receiptId, CaseId)),
                Store,
                new EfIntakeWorkStore(Factory),
                processor,
                TimeProvider.System,
                new NoUnidentified());

        private async Task<Guid> EnsureCaseAsync(PegasusDbContext context)
        {
            if (CaseId != Guid.Empty)
            {
                return CaseId;
            }

            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            CaseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Role = "principal", Id = organizationId, Name = "Sweep listing test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = DateTimeOffset.UtcNow },
                new PrincipalEntity
                {
                    Id = principalId, OrganizationId = organizationId, SequenceLineageId = lineageId,
                    Code = "SWEP", IsActive = true, Version = 0
                },
                new CaseEntity
                {
                    Id = CaseId, PrincipalId = principalId, SequenceLineageId = lineageId,
                    Year = 2031, Sequence = nextCaseSequence++, Reference = "SWEP31001", Type = "audit",
                    InitialState = "NotReady", CustodyState = "Pending", CreatedAtUtc = DateTimeOffset.UtcNow,
                    Version = 1, ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = CaseId, State = "NotReady", Version = 1, ConcurrencyToken = Guid.NewGuid()
                });
            return CaseId;
        }

        private static IntakeStagedReceiptEntity ImageStagedReceipt(
            Guid id, string token, string hash, DateTimeOffset receivedAtUtc) => new()
        {
            Id = id, SourceFileName = "case.jpg", MediaType = "image/jpeg", SourceLength = 3, SourceHash = hash,
            SourceChannel = "manual_upload", ExternalReceiptToken = token, ReceivedAtUtc = receivedAtUtc,
            Actor = "staff:test", StorageKey = $"manual/{token}", StagedAtUtc = receivedAtUtc
        };

        private static IntakeReceiptEntity ImageReceipt(
            Guid id, string token, string hash, DateTimeOffset receivedAtUtc, string mediaType = "image/jpeg") => new()
        {
            Id = id, SourceFileName = "case.jpg", MediaType = mediaType, SourceLength = 3, SourceHash = hash,
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

        public async Task AddExistingOutcomeAsync(string kind)
        {
            await using var context = await Database.CreateContextAsync();
            if (kind == "allocation")
            {
                context.IntakeAllocationAttempts.Add(new()
                {
                    Id = Guid.NewGuid(), IntakeReceiptId = ReceiptId, AttemptNumber = 1,
                    Kind = "automatic", Status = "succeeded", ExpectedReceiptVersion = 4,
                    PrincipalCode = "TEST", ActorKind = "Staff", ActorSubjectId = Actor.SubjectId,
                    ActorRolesJson = "[\"Engineer\"]", OperationKey = $"allocation:{Guid.NewGuid():N}",
                    CommandHash = new string('a', 64), Reason = "seed", StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                context.ImageIntakes.Add(new()
                {
                    Id = Guid.NewGuid(), OriginReceiptId = ReceiptId, SourceChannel = "manual_upload",
                    ExternalReceiptToken = "discard-test", SourceHash = new string('a', 64),
                    EvaluationRevisionId = Guid.NewGuid(), SubmissionGroupId = GroupId,
                    NormalizedVehicleRegistration = "AB12CDE", ImageIntakeReference = "IMG-TEST-1",
                    CreatedAtUtc = DateTimeOffset.UtcNow, CreatedByActorKind = "Staff",
                    CreatedByActorSubjectId = Actor.SubjectId, Reason = "seed",
                    CreationOperationKey = $"image:{Guid.NewGuid():N}", RequestFingerprint = new string('a', 64),
                    LifecycleState = "registered", LifecycleVersion = 1
                });
            }
            await context.SaveChangesAsync();
        }

        public async Task AssertActionableAsync()
        {
            await using var context = await Database.CreateContextAsync();
            var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == GroupId);
            Assert.Null(group.DiscardedAtUtc);
            Assert.Empty(await context.IntakeSubmissionGroupHistory.ToListAsync());
        }

        private async Task SeedAsync()
        {
            await using var context = await Database.CreateContextAsync();
            context.AddRange(
                new IntakeStagedReceiptEntity
                {
                    Id = StagedReceiptId, SourceFileName = "discard-test.jpg", MediaType = "image/jpeg",
                    SourceLength = 3, SourceHash = new string('a', 64), SourceChannel = "manual_upload",
                    ExternalReceiptToken = "discard-test", ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Actor = "staff:test", StorageKey = "manual/discard-test", StagedAtUtc = DateTimeOffset.UtcNow
                },
                new IntakeReceiptEntity
                {
                    Id = ReceiptId, SourceFileName = "discard-test.jpg", MediaType = "image/jpeg", SourceLength = 3,
                    SourceHash = new string('a', 64), SourceChannel = "manual_upload", ExternalReceiptToken = "discard-test",
                    ReceivedAtUtc = DateTimeOffset.UtcNow, ProcessedAtUtc = DateTimeOffset.UtcNow,
                    SourceReaderKey = "test", SourceReaderVersion = "1", Version = 4,
                    Decision = "needs_sorting", DecisionReason = "test", EvidenceJson = "[]", FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new IntakeSubmissionGroupEntity
                {
                    Id = GroupId, SourceChannel = "manual_upload", SubmissionToken = "discard-test",
                    ExpectedMemberCount = 1, Actor = "staff:test", ReceivedAtUtc = DateTimeOffset.UtcNow, Version = 1
                },
                new IntakeSubmissionGroupMemberEntity
                {
                    Id = Guid.NewGuid(), GroupId = GroupId, Ordinal = 0, StagedReceiptId = StagedReceiptId,
                    SourceFileName = "discard-test.jpg", SourceHash = new string('a', 64), AddedAtUtc = DateTimeOffset.UtcNow
                },
                new IntakeWorkItemEntity
                {
                    Id = Guid.NewGuid(), StagedReceiptId = StagedReceiptId, OperationKey = "manual-upload:discard-test:0",
                    State = "completed", AttemptCount = 1, DueAtUtc = DateTimeOffset.UtcNow,
                    ProcessedReceiptId = ReceiptId, CompletedAtUtc = DateTimeOffset.UtcNow
                });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await Database.DisposeAsync();
    }

    private sealed class FailDiscardSaveInterceptor : SaveChangesInterceptor
    {
        private int fail = 1;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<IntakeSubmissionGroupHistoryEntity>()
                    .Any(entry => entry.State == EntityState.Added) == true
                && Interlocked.Exchange(ref fail, 0) == 1)
            {
                throw new DbUpdateException("Injected discard transaction failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record SeededReceipt(Guid ReceiptId, Guid StagedReceiptId);

    private static IntakeReceipt AssociatedReceipt(Guid receiptId, Guid caseId) => new(
        receiptId,
        "case.jpg",
        "image/jpeg",
        3,
        new string('d', 64),
        new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, receiptId.ToString("N")),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        IntakeDecision.NeedsSorting,
        "test",
        [],
        [],
        null,
        [],
        null,
        null,
        false,
        "test",
        "1",
        null,
        null,
        ManualLinkedCaseId: caseId,
        ManualAssociationVersion: 0);

    private sealed class FixedReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(id == receipt.Id ? receipt : null);
    }

    private sealed class CountingProcessor : IProcessQueuedIntake
    {
        public int Calls { get; private set; }

        public Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(QueuedIntakeProcessingOutcome.Completed);
        }
    }

    private sealed class ThrowingProcessor(Exception exception) : IProcessQueuedIntake
    {
        public int Calls { get; private set; }

        public Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromException<QueuedIntakeProcessingOutcome>(exception);
        }
    }

    private sealed class NoUnidentified : IRegisterUnidentified
    {
        public Task<UnidentifiedRegisterResult> ExecuteAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A Case-associated replay must not be registered Unidentified.");
    }
}
