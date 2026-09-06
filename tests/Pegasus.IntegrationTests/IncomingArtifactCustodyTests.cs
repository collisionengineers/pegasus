using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The public-upload half of <see cref="RetainIncomingArtifact"/>'s store port
/// over the real database.
/// </summary>
/// <remarks>
/// The command's own invariants are proved without a database in
/// <c>Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs</c>. What needs
/// SQL is what the store writes and reads back, which is what these cover. The
/// store is exercised directly because these are its own invariants; the
/// accept path that composes it — the public upload page — is proved end to
/// end in <see cref="PublicUploadRetentionWebTests"/>.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed class IncomingArtifactCustodyTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Two occurrences can share one document version — two arrivals of the
    /// same file are two occurrences, and custody may return the same logical
    /// version for both. Recording a later Pending occurrence must not erase
    /// the confirmed remote identities the first one earned.
    /// </summary>
    [Fact]
    public async Task APendingRecordAfterAConfirmedOneLeavesTheRemoteIdentitiesIntact()
    {
        using var factory = new IntakeWebApplicationFactory();
        var seeded = await SeedSessionAsync(factory.Services);
        var store = new EfPublicUploadRetentionStore(
            factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>());

        await store.RecordAsync(
            new(
                seeded.FirstOccurrenceId,
                seeded.FirstOperationKey,
                IncomingArtifactCustodyState.Confirmed,
                seeded.CaseId,
                seeded.DocumentId,
                seeded.DocumentVersionId,
                "box-file-1",
                "box-version-1"),
            CancellationToken.None);

        Assert.Equal(("box-file-1", "box-version-1"), await ReadIdentitiesAsync(factory.Services, seeded.DocumentVersionId));

        // The second occurrence is handed over and comes back Pending, with no
        // remote identity of its own, against the same version.
        await store.RecordAsync(
            new(
                seeded.SecondOccurrenceId,
                seeded.SecondOperationKey,
                IncomingArtifactCustodyState.Pending,
                seeded.CaseId,
                seeded.DocumentId,
                seeded.DocumentVersionId),
            CancellationToken.None);

        // The first occurrence's confirmed identities are still true, so they
        // are still there.
        Assert.Equal(("box-file-1", "box-version-1"), await ReadIdentitiesAsync(factory.Services, seeded.DocumentVersionId));

        var first = await store.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
        Assert.NotNull(first);
        Assert.True(first.IsConfirmed);
        Assert.Equal("box-file-1", first.BoxFileId);
        Assert.Equal("box-version-1", first.BoxVersionId);

        var second = await store.FindAsync(seeded.SecondOperationKey, CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(IncomingArtifactCustodyState.Pending, second.State);
        Assert.False(second.IsConfirmed);
    }

    [Theory]
    [InlineData(IncomingArtifactCustodyState.Pending)]
    [InlineData(IncomingArtifactCustodyState.Failed)]
    [InlineData(IncomingArtifactCustodyState.Unknown)]
    public async Task ANonConfirmedRecordNeverWritesARemoteIdentity(
        IncomingArtifactCustodyState state)
    {
        using var factory = new IntakeWebApplicationFactory();
        var seeded = await SeedSessionAsync(factory.Services);
        var store = new EfPublicUploadRetentionStore(
            factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>());

        // Even when a disposition arrives carrying identities, anything but
        // Confirmed must not assert that custody holds the bytes.
        await store.RecordAsync(
            new(
                seeded.FirstOccurrenceId,
                seeded.FirstOperationKey,
                state,
                seeded.CaseId,
                seeded.DocumentId,
                seeded.DocumentVersionId,
                "box-file-unproven",
                "box-version-unproven"),
            CancellationToken.None);

        Assert.Equal((null, null), await ReadIdentitiesAsync(factory.Services, seeded.DocumentVersionId));
        var found = await store.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(state, found.State);
        Assert.False(found.IsConfirmed);
        Assert.Null(found.BoxFileId);
        Assert.Null(found.BoxVersionId);
    }

    /// <summary>
    /// A committed arrival is visible as the uncertain thing it is, and it can
    /// be claimed exactly once. The claim is the whole of what decides which
    /// caller offers the bytes, so a second caller of the same arrival is told
    /// no and has an arrival to reconcile instead of a null to hand over
    /// against.
    /// </summary>
    [Fact]
    public async Task AnArrivalIsReportedAsUncertainAndClaimedExactlyOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        var seeded = await SeedSessionAsync(factory.Services);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var store = new EfPublicUploadRetentionStore(contextFactory);

        // Custody has said nothing about it, which is exactly Unknown - not
        // the Pending custody gives, and not nothing at all.
        var arrived = await store.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
        Assert.NotNull(arrived);
        Assert.Equal(IncomingArtifactCustodyState.Unknown, arrived.State);
        Assert.Equal(seeded.FirstOccurrenceId, arrived.OccurrenceId);

        Assert.True(await store.TryClaimHandOverAsync(
            seeded.FirstOccurrenceId,
            CancellationToken.None));

        // Committed before the hand-over, so a crash from here on leaves an
        // arrival to ask about rather than one to offer again.
        Assert.Equal(
            EfPublicUploadRetentionStore.UnknownCode,
            await ReadCustodyStateAsync(factory.Services, seeded.FirstOccurrenceId));

        // Everyone else, whenever they ask.
        Assert.False(await store.TryClaimHandOverAsync(
            seeded.FirstOccurrenceId,
            CancellationToken.None));

        await store.RecordAsync(
            new(
                seeded.FirstOccurrenceId,
                seeded.FirstOperationKey,
                IncomingArtifactCustodyState.Pending,
                seeded.CaseId,
                seeded.DocumentId,
                seeded.DocumentVersionId),
            CancellationToken.None);

        var found = await store.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(IncomingArtifactCustodyState.Pending, found.State);
        Assert.False(await store.TryClaimHandOverAsync(
            seeded.FirstOccurrenceId,
            CancellationToken.None));
    }

    /// <summary>
    /// Confirmation only moves forward. A recorder that arrives after custody
    /// has answered knows less than the row does - the lost response, the
    /// retry that could not read status - and must not be able to pull a
    /// confirmed retention back to uncertain, or to failed, or to strip the
    /// identities that say where the bytes are.
    /// </summary>
    [Theory]
    [InlineData(IncomingArtifactCustodyState.Unknown)]
    [InlineData(IncomingArtifactCustodyState.Pending)]
    [InlineData(IncomingArtifactCustodyState.Failed)]
    public async Task ALateRecorderNeverPullsAConfirmedRetentionBack(
        IncomingArtifactCustodyState late)
    {
        using var factory = new IntakeWebApplicationFactory();
        var seeded = await SeedSessionAsync(factory.Services);
        var store = new EfPublicUploadRetentionStore(
            factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>());

        await store.RecordAsync(
            new(
                seeded.FirstOccurrenceId,
                seeded.FirstOperationKey,
                IncomingArtifactCustodyState.Confirmed,
                seeded.CaseId,
                seeded.DocumentId,
                seeded.DocumentVersionId,
                "box-file-1",
                "box-version-1"),
            CancellationToken.None);

        await store.RecordAsync(
            new(
                seeded.FirstOccurrenceId,
                seeded.FirstOperationKey,
                late,
                seeded.CaseId),
            CancellationToken.None);

        var found = await store.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
        Assert.NotNull(found);
        Assert.True(found.IsConfirmed);
        Assert.Equal(seeded.DocumentId, found.DocumentId);
        Assert.Equal(seeded.DocumentVersionId, found.DocumentVersionId);
        Assert.Equal(
            ("box-file-1", "box-version-1"),
            await ReadIdentitiesAsync(factory.Services, seeded.DocumentVersionId));
    }

    /// <summary>
    /// The same rule under the concurrency it exists for. A caller that lost
    /// the claim reconciles to Pending while the winner is still inside
    /// custody, so two recorders are on one occurrence and each reads the row
    /// for itself: the rule has to be the database's and not each caller's,
    /// because a read-modify-write loses the winner's Confirmed to whichever
    /// UPDATE happens to land second.
    /// </summary>
    /// <remarks>
    /// Rounds, because which recorder lands first is the race's to decide, and
    /// the invariant holds either way: a Pending that arrives first is
    /// overtaken by the confirmation, and one that arrives second is refused.
    /// Two stores, each opening its own context per call, exactly as two
    /// requests do.
    /// </remarks>
    [Fact]
    public async Task ALatePendingWriteAgainstAConfirmedRowIsANoOpUnderConcurrency()
    {
        using var factory = new IntakeWebApplicationFactory();
        var seeded = await SeedSessionAsync(factory.Services);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var winner = new EfPublicUploadRetentionStore(contextFactory);
        var loser = new EfPublicUploadRetentionStore(contextFactory);
        var confirmed = new RetainedIncomingArtifact(
            seeded.FirstOccurrenceId,
            seeded.FirstOperationKey,
            IncomingArtifactCustodyState.Confirmed,
            seeded.CaseId,
            seeded.DocumentId,
            seeded.DocumentVersionId,
            "box-file-1",
            "box-version-1");

        // What a reconciliation carries: the same document, the state custody
        // had not finished, and no remote identity of its own.
        var pending = confirmed with
        {
            State = IncomingArtifactCustodyState.Pending,
            BoxFileId = null,
            BoxVersionId = null
        };

        for (var round = 0; round < 25; round++)
        {
            await ResetToArrivedAsync(factory.Services, seeded);
            using var start = new Barrier(2);
            var confirming = Task.Run(async () =>
            {
                start.SignalAndWait();
                await winner.RecordAsync(confirmed, CancellationToken.None);
            });
            var reconciling = Task.Run(async () =>
            {
                start.SignalAndWait();
                await loser.RecordAsync(pending, CancellationToken.None);
            });

            await Task.WhenAll(confirming, reconciling);

            var found = await winner.FindAsync(seeded.FirstOperationKey, CancellationToken.None);
            Assert.NotNull(found);
            Assert.True(
                found.IsConfirmed,
                $"round {round}: a confirmed retention was pulled back to '{found.State}'.");
            Assert.Equal(seeded.DocumentId, found.DocumentId);
            Assert.Equal(seeded.DocumentVersionId, found.DocumentVersionId);
            Assert.Equal(
                ("box-file-1", "box-version-1"),
                await ReadIdentitiesAsync(factory.Services, seeded.DocumentVersionId));
        }
    }

    /// <summary>
    /// Puts the occurrence and the version it points at back to the state a
    /// committed arrival starts in, so each round of the race is run against
    /// an unanswered arrival rather than the previous round's answer.
    /// </summary>
    private static async Task ResetToArrivedAsync(
        IServiceProvider services,
        SeededSession seeded)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Set<PublicUploadOccurrenceEntity>()
            .Where(item => item.Id == seeded.FirstOccurrenceId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.CustodyState, EfPublicUploadRetentionStore.ArrivedCode)
                .SetProperty(item => item.DocumentId, (Guid?)null)
                .SetProperty(item => item.DocumentVersionId, (Guid?)null));
        await context.Set<DocumentVersionEntity>()
            .Where(item => item.Id == seeded.DocumentVersionId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.BoxFileId, (string?)null)
                .SetProperty(item => item.BoxVersionId, (string?)null));
    }

    private static async Task<string> ReadCustodyStateAsync(
        IServiceProvider services,
        Guid occurrenceId)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.Id == occurrenceId)
            .Select(item => item.CustodyState)
            .SingleAsync();
    }

    private static async Task<(string? BoxFileId, string? BoxVersionId)> ReadIdentitiesAsync(
        IServiceProvider services,
        Guid versionId)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .Where(item => item.Id == versionId)
            .Select(item => ValueTuple.Create(item.BoxFileId, item.BoxVersionId))
            .SingleAsync();
    }

    private sealed record SeededSession(
        Guid CaseId,
        Guid DocumentId,
        Guid DocumentVersionId,
        Guid FirstOccurrenceId,
        string FirstOperationKey,
        Guid SecondOccurrenceId,
        string SecondOperationKey);

    /// <summary>
    /// One Case, one request-upload link, one submission session, one document
    /// version and two occurrences pointing at it. The receipt and Case
    /// fixtures are the suite's own, reused rather than copied.
    /// </summary>
    private static async Task<SeededSession> SeedSessionAsync(IServiceProvider services)
    {
        // The receipt store is scoped, so the seeding runs in a request scope
        // like every other caller of the suite's fixtures.
        await using var scope = services.CreateAsyncScope();
        var scopedServices = scope.ServiceProvider;
        var receiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(
            scopedServices,
            "incoming-artifact-custody.pdf");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(scopedServices, receiptId, "CUST01", "Review");

        var contextFactory = scopedServices.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var linkId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();

        context.Set<RequestUploadLinkEntity>().Add(new()
        {
            Id = linkId,
            CaseId = caseId,
            TokenDigest = new string('a', 64),
            Status = RequestUploadStatus.Active,
            CreatedAtUtc = Now,
            ExpiresAtUtc = Now.AddHours(1),
            LimitsVersion = "integration-fixture-v1",
            Version = 1,
            CreateOperationKey = $"request-create:{linkId:N}"
        });
        context.Set<PublicUploadSessionEntity>().Add(new()
        {
            Id = sessionId,
            RequestUploadLinkId = linkId,
            LimitsVersion = "integration-fixture-v1",
            StartedAtUtc = Now,
            ExpiresAtUtc = Now.Add(PublicUploadSessionPolicy.Window),
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        });
        context.Set<CaseDocumentEntity>().Add(new()
        {
            Id = documentId,
            CaseId = caseId,
            Ordinal = 2,
            SourceOccurrenceIdentity = $"request:{linkId:N}:custody-fixture"
        });
        context.Set<DocumentVersionEntity>().Add(new()
        {
            Id = versionId,
            DocumentId = documentId,
            Version = 1,
            FileName = "estimate.pdf",
            MediaType = "application/pdf",
            ContentLength = 1024,
            Sha256 = new string('b', 64),
            CustodyStatus = DocumentCustodyStatus.Confirmed,
            CreatedAtUtc = Now,
            CreatedBy = "request-upload",
            IsCurrent = true
        });

        // The port is addressed globally, so each occurrence's key is scoped by
        // its upload link exactly as the accept path scopes it.
        var firstKey = EfPublicUploadRetentionStore.ScopeOperationKey(linkId, "upload-1");
        var secondKey = EfPublicUploadRetentionStore.ScopeOperationKey(linkId, "upload-2");
        foreach (var (id, key) in new[] { (firstOccurrenceId, firstKey), (secondOccurrenceId, secondKey) })
        {
            context.Set<PublicUploadOccurrenceEntity>().Add(new()
            {
                Id = id,
                SessionId = sessionId,
                OperationKey = key,
                // Deliberately the same proposed name: two arrivals of the
                // same file are two occurrences, never one overwriting the
                // other.
                ProposedName = "estimate.pdf",
                MediaType = "application/pdf",
                Size = 1024,
                Sha256 = new string('b', 64),
                // The state an arrival actually starts in: committed, offered
                // to nobody yet, and claimable exactly once.
                CustodyState = EfPublicUploadRetentionStore.ArrivedCode
            });
        }

        await context.SaveChangesAsync();
        return new(caseId, documentId, versionId, firstOccurrenceId, firstKey, secondOccurrenceId, secondKey);
    }
}
