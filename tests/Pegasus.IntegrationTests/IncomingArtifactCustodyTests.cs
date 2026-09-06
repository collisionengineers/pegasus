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
                CustodyState = EfPublicUploadRetentionStore.ToCode(
                    IncomingArtifactCustodyState.Pending)
            });
        }

        await context.SaveChangesAsync();
        return new(caseId, documentId, versionId, firstOccurrenceId, firstKey, secondOccurrenceId, secondKey);
    }
}
