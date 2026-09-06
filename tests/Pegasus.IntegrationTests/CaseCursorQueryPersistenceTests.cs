using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047: the four B-owned cursor queries against a real SQL Server
/// engine and A's real <see cref="DataProtectionCursorProtector"/> (G9) —
/// keyset pagination that stays disjoint and complete across a page
/// boundary (including when several rows tie on the sort column),
/// terminates with a null <c>NextCursor</c>, enforces the limit bound, and
/// refuses a cursor minted for a different actor, filter set, order, or
/// case, or tampered with in transit. <see cref="SearchCasesByCursor"/>'s
/// own ordering is also proved identical to the numbered
/// <see cref="SearchCases"/> for the same filters.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseCursorQueryPersistenceTests
{
    private static readonly DateTimeOffset BaseUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Composes A's real protector the way <c>DataProtectionCursorTests</c>
    /// does — an ephemeral Data Protection provider, not a codec these tests
    /// own — so a store test proves the same cryptographic scope binding
    /// production wiring gets from <c>Program.cs</c>'s singleton
    /// registration.
    /// </summary>
    private static DataProtectionCursorProtector CreateProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        return new DataProtectionCursorProtector(services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public async Task SearchPagesAreDisjointAndCompleteAcrossABoundaryWithEqualSortValuesTiesBrokenById()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "CURS");
        var caseIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            caseIds.Add(await SeedCaseAsync(database, principalId, lineageId, $"CURS3100{i}", i + 1, BaseUtcNow));
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var search = new SearchCasesByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());
        var filters = new CaseSearchFilters(Principal: "CURS");

        var (seen, pages) = await DrainAsync(
            cursor => search.ExecuteAsync(new(actor, filters, Limit: 2, Cursor: cursor), CancellationToken.None),
            item => item.CaseId);

        // 5 rows tied on ReceivedAtUtc, limit 2: three pages (2, 2, 1), every
        // case seen exactly once — disjoint and complete, which is the
        // property the tie-break by id exists to guarantee. (SQL Server's
        // own uniqueidentifier ordering does not match .NET's Guid.CompareTo,
        // so the assertion is set equality, not a predicted byte order.)
        Assert.Equal(3, pages);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(caseIds.ToHashSet(), seen.ToHashSet());
    }

    [Fact]
    public async Task LimitOneHundredIsAcceptedAndOneHundredOneIsRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "LIMT");
        await SeedCaseAsync(database, principalId, lineageId, "LIMT31001", 1, BaseUtcNow);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var search = new SearchCasesByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());
        var filters = new CaseSearchFilters(Principal: "LIMT");

        var accepted = await search.ExecuteAsync(new(actor, filters, Limit: 100), CancellationToken.None);
        Assert.Single(accepted.Items);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => search.ExecuteAsync(
            new(actor, filters, Limit: 101), CancellationToken.None));
    }

    [Fact]
    public async Task ACursorIsRefusedForADifferentActorOrFilterSet()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "FRGN");
        for (var i = 0; i < 3; i++)
        {
            await SeedCaseAsync(database, principalId, lineageId, $"FRGN3100{i}", i + 1, BaseUtcNow);
        }

        var actorA = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var actorB = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var search = new SearchCasesByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());
        var filters = new CaseSearchFilters(Principal: "FRGN");

        var firstPage = await search.ExecuteAsync(new(actorA, filters, Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actorB, filters, Cursor: firstPage.NextCursor, Limit: 1), CancellationToken.None));

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actorA, filters with { Origin = "Email" }, Cursor: firstPage.NextCursor, Limit: 1),
            CancellationToken.None));

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actorA, filters, CaseSearchOrder.ReferenceAsc, firstPage.NextCursor, 1), CancellationToken.None));
    }

    [Fact]
    public async Task ATamperedRealTokenIsRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "TMPR");
        await SeedCaseAsync(database, principalId, lineageId, "TMPR31001", 1, BaseUtcNow);
        await SeedCaseAsync(database, principalId, lineageId, "TMPR31002", 2, BaseUtcNow);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var search = new SearchCasesByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());
        var filters = new CaseSearchFilters(Principal: "TMPR");

        var firstPage = await search.ExecuteAsync(new(actor, filters, Limit: 1), CancellationToken.None);
        var cursor = firstPage.NextCursor;
        Assert.NotNull(cursor);
        var tampered = cursor[..^1] + (cursor[^1] == 'A' ? 'B' : 'A');

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actor, filters, Cursor: tampered, Limit: 1), CancellationToken.None));
    }

    [Fact]
    public async Task CursorResultsMatchTheNumberedSearchOrderingForTheSameFilters()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "MTCH");
        var expectedOrder = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            // Distinct ReceivedAtUtc per row, so neither endpoint's tie-break
            // choice can matter — this proves ordering parity, not tie
            // handling (that is the disjoint/complete test above).
            expectedOrder.Add(await SeedCaseAsync(
                database, principalId, lineageId, $"MTCH3100{i}", i + 1, BaseUtcNow.AddMinutes(-i)));
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICaseQueryStore>();
        var numbered = new SearchCases(store);
        var cursorSearch = new SearchCasesByCursor(store, CreateProtector());
        var filters = new CaseSearchFilters(Principal: "MTCH");

        var numberedResult = await numbered.ExecuteAsync(new(actor, filters, 1, 50), CancellationToken.None);

        var (cursorItems, _) = await DrainAsync(
            cursor => cursorSearch.ExecuteAsync(
                new(actor, filters, Limit: 2, Cursor: cursor), CancellationToken.None),
            item => item.CaseId);

        Assert.Equal(expectedOrder, numberedResult.Items.Select(item => item.CaseId).ToList());
        Assert.Equal(numberedResult.Items.Select(item => item.CaseId).ToList(), cursorItems);
    }

    [Fact]
    public async Task DocumentPagesAreDisjointAndCompleteAcrossABoundaryWithEqualSortValuesTiesBrokenById()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "DOCS");
        var caseId = await SeedCaseAsync(database, principalId, lineageId, "DOCS31001", 1, BaseUtcNow);
        var occurrenceIds = new List<Guid>();
        await using (var context = await database.CreateContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                var documentId = Guid.NewGuid();
                var versionId = Guid.NewGuid();
                var occurrenceId = Guid.NewGuid();
                occurrenceIds.Add(occurrenceId);
                context.AddRange(
                    new CaseDocumentEntity
                    {
                        Id = documentId,
                        CaseId = caseId,
                        Ordinal = i,
                        SourceOccurrenceIdentity = $"cursor-doc:{documentId:N}"
                    },
                    new DocumentVersionEntity
                    {
                        Id = versionId,
                        DocumentId = documentId,
                        Version = 1,
                        FileName = $"doc-{i}.pdf",
                        MediaType = "application/pdf",
                        ContentLength = 1,
                        Sha256 = new string('a', 64),
                        CustodyStatus = DocumentCustodyStatus.Confirmed,
                        CreatedAtUtc = BaseUtcNow,
                        CreatedBy = "Staff:test",
                        IsCurrent = true
                    },
                    new DocumentOccurrenceEntity
                    {
                        Id = occurrenceId,
                        CaseId = caseId,
                        DocumentId = documentId,
                        VersionId = versionId,
                        SemanticRole = DocumentSemanticRole.Image,
                        Source = DocumentSource.StaffUpload,
                        SourceOccurrenceIdentity = $"cursor-doc:{documentId:N}",
                        RecordedAtUtc = BaseUtcNow,
                        OperationKey = $"seed-doc:{documentId:N}"
                    });
            }
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var list = new ListCaseDocumentsByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());

        var (seen, pages) = await DrainAsync(
            cursor => list.ExecuteAsync(new(actor, caseId, cursor, 2), CancellationToken.None),
            item => item.Occurrence.Id);

        // The page unit is the occurrence: one row per occurrence, newest
        // first, id tie-break — 5 occurrences at limit 2 is three pages with
        // every occurrence seen exactly once.
        Assert.Equal(3, pages);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(occurrenceIds.ToHashSet(), seen.ToHashSet());
    }

    /// <summary>
    /// CASE-047, Stream A MCP review: the regression behind the occurrence
    /// page unit — one document carrying more occurrences than the caller's
    /// limit. A document-unit page would return that document once with a
    /// single occurrence and lose the rest; the occurrence-unit page
    /// enumerates every occurrence exactly once, each paired with the exact
    /// version it names, newest first, and terminates.
    /// </summary>
    [Fact]
    public async Task ADocumentWithMoreOccurrencesThanTheLimitEnumeratesEveryOccurrence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "OCCR");
        var caseId = await SeedCaseAsync(database, principalId, lineageId, "OCCR31001", 1, BaseUtcNow);
        var documentId = Guid.NewGuid();
        var expected = new List<(Guid OccurrenceId, Guid VersionId, DateTimeOffset RecordedAtUtc)>();
        await using (var context = await database.CreateContextAsync())
        {
            context.Add(new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                Ordinal = 0,
                SourceOccurrenceIdentity = $"cursor-occ:{documentId:N}"
            });
            for (var i = 0; i < 5; i++)
            {
                var versionId = Guid.NewGuid();
                var occurrenceId = Guid.NewGuid();
                var recordedAtUtc = BaseUtcNow.AddMinutes(-i);
                expected.Add((occurrenceId, versionId, recordedAtUtc));
                context.AddRange(
                    new DocumentVersionEntity
                    {
                        Id = versionId,
                        DocumentId = documentId,
                        Version = i + 1,
                        FileName = $"doc-v{i + 1}.pdf",
                        MediaType = "application/pdf",
                        ContentLength = 1 + i,
                        Sha256 = new string((char)('a' + i), 64),
                        CustodyStatus = DocumentCustodyStatus.Confirmed,
                        CreatedAtUtc = recordedAtUtc,
                        CreatedBy = "Staff:test",
                        IsCurrent = i == 4
                    },
                    new DocumentOccurrenceEntity
                    {
                        Id = occurrenceId,
                        CaseId = caseId,
                        DocumentId = documentId,
                        VersionId = versionId,
                        SemanticRole = DocumentSemanticRole.Image,
                        Source = DocumentSource.StaffUpload,
                        SourceOccurrenceIdentity = $"cursor-occ:{documentId:N}:{i}",
                        RecordedAtUtc = recordedAtUtc,
                        OperationKey = $"seed-occ:{occurrenceId:N}"
                    });
            }
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var list = new ListCaseDocumentsByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());

        // Limit 1 — the exact shape of the reported loss: one document,
        // many occurrences, a page that cannot hold them.
        var (seen, pages) = await DrainAsync(
            cursor => list.ExecuteAsync(new(actor, caseId, cursor, 1), CancellationToken.None),
            item => item.Occurrence.Id);

        Assert.Equal(5, pages);
        Assert.Equal(5, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(expected.Select(row => row.OccurrenceId).ToHashSet(), seen.ToHashSet());

        var items = new List<CaseDocumentPageItem>();
        string? cursor = null;
        do
        {
            var page = await list.ExecuteAsync(new(actor, caseId, cursor, 1), CancellationToken.None);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        // Newest first, every item is one occurrence of the same document
        // paired with the exact version that occurrence names — a
        // flatten-and-take-first consumer loses nothing.
        Assert.Equal(
            expected.OrderByDescending(row => row.RecordedAtUtc).Select(row => row.OccurrenceId).ToArray(),
            items.Select(item => item.Occurrence.Id).ToArray());
        Assert.All(items, item => Assert.Equal(documentId, item.Occurrence.DocumentId));
        Assert.All(items, item => Assert.Equal(item.Occurrence.VersionId, item.Version.Id));
    }

    [Fact]
    public async Task HistoryPagesAreDisjointAndCompleteAcrossABoundaryWithEqualSortValuesTiesBrokenById()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "HIST");
        var caseId = await SeedCaseAsync(database, principalId, lineageId, "HIST31001", 1, BaseUtcNow);
        var entryIds = new List<Guid>();
        await using (var context = await database.CreateContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                var entryId = Guid.NewGuid();
                entryIds.Add(entryId);
                context.Add(new CaseWorkflowEventEntity
                {
                    Id = entryId,
                    CaseId = caseId,
                    EventType = "cursor_test_event",
                    OperationKey = $"cursor-history:{entryId:N}",
                    RequestHash = new string('0', 64),
                    ActorKind = nameof(ActorKind.Staff),
                    ActorSubjectId = Guid.NewGuid().ToString(),
                    ActorRolesJson = "[]",
                    Reason = "Cursor history fixture.",
                    OccurredAtUtc = BaseUtcNow,
                    BeforeVersion = i,
                    AfterVersion = i + 1
                });
            }
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var list = new ListCaseHistoryByCursor(
            scope.ServiceProvider.GetRequiredService<ICaseQueryStore>(), CreateProtector());

        var (seen, pages) = await DrainAsync(
            cursor => list.ExecuteAsync(new(actor, caseId, cursor, 2), CancellationToken.None),
            item => item.EntryId);

        Assert.Equal(3, pages);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(entryIds.ToHashSet(), seen.ToHashSet());
    }

    [Fact]
    public async Task EstimatePagesAreCompleteInVersionOrderAndACursorIsRefusedForAnotherCase()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "ESTM");
        var caseId = await SeedCaseAsync(database, principalId, lineageId, "ESTM31001", 1, BaseUtcNow);
        var otherCaseId = await SeedCaseAsync(database, principalId, lineageId, "ESTM31002", 2, BaseUtcNow);
        await using (var context = await database.CreateContextAsync())
        {
            for (var version = 1; version <= 3; version++)
            {
                context.Add(new CaseRepairSpecificationEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Version = version,
                    State = nameof(RepairSpecificationState.Draft),
                    SourceRoute = nameof(RepairSpecificationSourceRoute.Manual),
                    CreatedBy = "Staff:test",
                    CreationOperationKey = $"cursor-estimate:{Guid.NewGuid():N}",
                    CreatedAtUtc = BaseUtcNow,
                    Name = $"Estimate {version}",
                    VatPercent = 20m
                });
            }
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var list = new ListCaseEstimatesByCursor(
            scope.ServiceProvider.GetRequiredService<IRepairSpecificationStore>(), CreateProtector());

        var firstPage = await list.ExecuteAsync(new(actor, caseId, Limit: 2), CancellationToken.None);
        Assert.Equal([3, 2], firstPage.Items.Select(item => item.Version).ToArray());
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await list.ExecuteAsync(new(actor, caseId, firstPage.NextCursor, 2), CancellationToken.None);
        Assert.Equal([1], secondPage.Items.Select(item => item.Version).ToArray());
        Assert.Null(secondPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => list.ExecuteAsync(
            new(actor, otherCaseId, firstPage.NextCursor, 2), CancellationToken.None));
    }

    /// <summary>
    /// CASE-047, Stream A review: <see cref="ICaseQueryStore.GetHeaderAsync"/>
    /// returns the same summary/workflow facts <see cref="GetCase"/> would,
    /// with the document, history and open-task lists reduced to counts.
    /// </summary>
    [Fact]
    public async Task GetHeaderReturnsCountsWithoutMaterializingTheFullDetails()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (_, lineageId, principalId) = await SeedPrincipalAsync(database, "HEAD");
        var caseId = await SeedCaseAsync(database, principalId, lineageId, "HEAD31001", 1, BaseUtcNow);
        await using (var context = await database.CreateContextAsync())
        {
            for (var i = 0; i < 2; i++)
            {
                var documentId = Guid.NewGuid();
                context.Add(new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = i,
                    SourceOccurrenceIdentity = $"header-doc:{documentId:N}"
                });
            }
            for (var i = 0; i < 3; i++)
            {
                var entryId = Guid.NewGuid();
                context.Add(new CaseWorkflowEventEntity
                {
                    Id = entryId,
                    CaseId = caseId,
                    EventType = "header_test_event",
                    OperationKey = $"header-history:{entryId:N}",
                    RequestHash = new string('0', 64),
                    ActorKind = nameof(ActorKind.Staff),
                    ActorSubjectId = Guid.NewGuid().ToString(),
                    ActorRolesJson = "[]",
                    Reason = "Header fixture.",
                    OccurredAtUtc = BaseUtcNow,
                    BeforeVersion = i,
                    AfterVersion = i + 1
                });
            }
            context.AddRange(
                new CaseTaskEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Description = "Open header task",
                    State = nameof(CaseTaskState.Open),
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseTaskEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Description = "Completed header task",
                    State = nameof(CaseTaskState.Completed),
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var getHeader = new GetCaseHeader(scope.ServiceProvider.GetRequiredService<ICaseQueryStore>());

        var header = await getHeader.ExecuteAsync(new(caseId, actor), CancellationToken.None);

        Assert.NotNull(header);
        Assert.Equal(caseId, header!.Summary.CaseId);
        Assert.Equal(caseId, header.Workflow.CaseId);
        Assert.Null(header.ActiveEditLease);
        Assert.Equal(2, header.DocumentCount);
        Assert.Equal(3, header.HistoryCount);
        Assert.Equal(1, header.OpenTaskCount);
    }

    [Fact]
    public async Task GetHeaderReturnsNullForACaseThatDoesNotExist()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var getHeader = new GetCaseHeader(scope.ServiceProvider.GetRequiredService<ICaseQueryStore>());

        var header = await getHeader.ExecuteAsync(new(Guid.NewGuid(), actor), CancellationToken.None);

        Assert.Null(header);
    }

    /// <summary>
    /// Walks a cursor query to its last page, collecting each row's identity
    /// in page order, and returns how many pages that took. The page guard
    /// stops a cursor that never terminates from hanging the suite.
    /// </summary>
    private static async Task<(IReadOnlyList<Guid> Seen, int Pages)> DrainAsync<T>(
        Func<string?, Task<CursorPage<T>>> readPage,
        Func<T, Guid> identify)
    {
        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await readPage(cursor);
            seen.AddRange(page.Items.Select(identify));
            pages++;
            Assert.True(pages < 10, "Runaway pagination — the cursor never reached the last page.");
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return (seen, pages);
    }

    private static async Task<(Guid OrganizationId, Guid LineageId, Guid PrincipalId)> SeedPrincipalAsync(
        LocalDbTestDatabase database, string principalCode)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = $"{principalCode} test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = BaseUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = principalCode,
                IsActive = true,
                Version = 0
            });
        await context.SaveChangesAsync();
        return (organizationId, lineageId, principalId);
    }

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        Guid principalId,
        Guid lineageId,
        string reference,
        int sequence,
        DateTimeOffset receivedAtUtc)
    {
        await using var context = await database.CreateContextAsync();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = $"{reference}.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"cursor:{receiptId:N}",
                ReceivedAtUtc = receivedAtUtc,
                ProcessedAtUtc = receivedAtUtc,
                SourceReaderKey = "cursor-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Cursor test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = sequence,
                Reference = reference,
                Type = "Audit",
                InitialState = "NotReady",
                CustodyState = "Pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = receivedAtUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "Review",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }
}
