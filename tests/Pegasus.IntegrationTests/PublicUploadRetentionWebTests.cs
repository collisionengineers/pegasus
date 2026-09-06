using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The public upload page as <see cref="RetainIncomingArtifact"/>'s production
/// caller, over the real database.
/// </summary>
/// <remarks>
/// <para>
/// What is proved here is the whole accept path a third party actually walks:
/// a POST to <c>/Uploads/{token}</c> starts or reuses the link's submission
/// session, records the arrival before custody is asked, hands the bytes over
/// under the link's own authority, and then records only what custody
/// answered. The store no longer creates a document, a version or a content
/// write of its own, so a confirmed custody is the only thing that can produce
/// a confirmed document.
/// </para>
/// <para>
/// The custody adapter itself is Stream A's (A04). The double registered here
/// stands in for it and <em>enforces A's authorization rule rather than
/// assuming it</em>: it re-reads the upload-link row and refuses anything but
/// a request-link actor naming that exact persisted link, with that link's own
/// Case, while the link is active, unrevoked and unexpired. A caller that
/// passed a document-request identity, a sender-supplied Case, or a holding
/// destination would fail here exactly as it will fail against A04.
/// </para>
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed partial class PublicUploadRetentionWebTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private const string LimitsVersion = "integration-fixture-v1";
    private static readonly byte[] Evidence = "public upload retention evidence"u8.ToArray();

    // The three sentences the public page can end on. They are asserted
    // verbatim because which one is shown is the whole of what the sender is
    // told about custody.
    private const string RetainedMessage =
        "Your document was received and retained securely.";

    private const string StoringMessage =
        "Your document was received and is being stored.";

    private const string RetryMessage =
        "The document could not be retained. Try again using the same upload operation.";

    [Fact]
    public async Task AConfirmedHandOverOpensTheFixedWindowAndRecordsTheBoxIdentities()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        var link = await SeedLinkAsync(factory.Services);

        var result = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains(RetainedMessage, result.CompletionBody, StringComparison.Ordinal);
        var call = Assert.Single(custody.Calls);

        // Stream A's rule, observed from inside the adapter: the authority is
        // the persisted link row and the Case is that link's own.
        Assert.Equal(ActorKind.RequestLink, call.ActorKind);
        Assert.Equal(link.LinkId.ToString("D"), call.ActorSubjectId);
        Assert.Equal(link.CaseId, call.CaseId);
        Assert.Null(call.IntakeReceiptId);
        Assert.Equal($"request:{link.LinkId:N}:{result.OperationKey}", call.OperationKey);
        Assert.Equal("evidence.txt", call.FileName);
        Assert.Equal(Evidence.Length, call.ObservedContentLength);
        Assert.Equal(Sha256Hex(Evidence), call.ObservedSha256);

        // The arrival was durable before custody was asked, and it carried the
        // pre-custody state at that moment — not a Pending custody has not
        // given, and certainly not a Confirmed one.
        Assert.Equal("arrived", call.CustodyStateAtHandOver);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);
        Assert.Equal(Now, session.StartedAtUtc);
        Assert.Equal(Now.Add(PublicUploadSessionPolicy.Window), session.ExpiresAtUtc);
        Assert.Null(session.FinalizedAtUtc);
        Assert.Equal(LimitsVersion, session.LimitsVersion);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Equal(call.DocumentId, occurrence.DocumentId);
        Assert.Equal(call.VersionId, occurrence.DocumentVersionId);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal(call.BoxFileId, version.BoxFileId);
        Assert.Equal(call.BoxVersionId, version.BoxVersionId);

        var accepted = await ReadLinkTotalsAsync(context, link.LinkId);
        Assert.Equal((1, (long)Evidence.Length), accepted);
    }

    /// <summary>
    /// A durable Pending hand-over is accepted — the bytes are held — but it
    /// asserts nothing about custody: no remote identity, and the fixed window
    /// stays shut until something is actually confirmed.
    /// </summary>
    [Fact]
    public async Task APendingHandOverIsAcceptedWithNoRemoteIdentityAndNoOpenWindow()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var result = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);

        // The one sentence the sender reads. Pending says the document arrived
        // and is being stored; it must not claim a custody custody has not
        // confirmed.
        Assert.Contains(StoringMessage, result.CompletionBody, StringComparison.Ordinal);
        Assert.DoesNotContain(RetainedMessage, result.CompletionBody, StringComparison.Ordinal);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);
        Assert.Null(session.StartedAtUtc);
        Assert.Null(session.ExpiresAtUtc);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal("pending", occurrence.CustodyState);
        Assert.NotNull(occurrence.DocumentVersionId);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Pending, version.CustodyStatus);
        Assert.Null(version.BoxFileId);
        Assert.Null(version.BoxVersionId);
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// A refused hand-over is not an upload. The page says so, the occurrence
    /// records the refusal, and nothing is counted against the link.
    /// </summary>
    [Theory]
    [InlineData(CaseArtifactCustodyDisposition.Failed, "failed")]
    [InlineData(CaseArtifactCustodyDisposition.Unknown, "unknown")]
    public async Task ARefusedOrUncertainHandOverIsNeverAcceptedAndNeverCounted(
        CaseArtifactCustodyDisposition disposition,
        string expectedState)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = disposition;
        var link = await SeedLinkAsync(factory.Services);

        var result = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(RetryMessage, result.Body, StringComparison.Ordinal);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);
        Assert.Null(session.StartedAtUtc);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal(expectedState, occurrence.CustodyState);
        Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The same operation key is one retention. The second submission returns
    /// the receipt the first one earned and never offers the bytes again, so
    /// the same logical document and version stand.
    /// </summary>
    [Fact]
    public async Task ReplayOfTheSameOperationKeyReturnsTheSameDocumentAndCallsCustodyOnce()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        var call = Assert.Single(custody.Calls);

        await using var context = await CreateContextAsync(factory.Services);
        var occurrence = Assert.Single(await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.OperationKey.StartsWith($"request:{link.LinkId:N}:"))
            .ToArrayAsync());
        Assert.Equal(call.DocumentId, occurrence.DocumentId);
        Assert.Equal(call.VersionId, occurrence.DocumentVersionId);
        Assert.Single(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());

        // A replay is not a second file: the link's accepted totals stand
        // where the first submission left them.
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The link's accepted totals rest on the committed occurrence, not on the
    /// receipt. A hand-over that earns no receipt — here because the adapter
    /// created no document occurrence for it — re-enters the accept path in
    /// full on a replay, and must still count exactly one file.
    /// </summary>
    [Fact]
    public async Task AReplayedArrivalThatEarnedNoReceiptIsStillCountedExactlyOnce()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        custody.CreatesDocumentOccurrence = false;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(RetainedMessage, second.CompletionBody, StringComparison.Ordinal);

        // The bytes were offered once: the confirmed retention answered the
        // replay without a second hand-over.
        Assert.Single(custody.Calls);

        await using var context = await CreateContextAsync(factory.Services);
        Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Single(await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.OperationKey.StartsWith($"request:{link.LinkId:N}:"))
            .ToArrayAsync());

        // One occurrence, one file, whatever the receipt does or does not say.
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
        var status = await context.Set<RequestUploadLinkEntity>()
            .AsNoTracking()
            .Where(item => item.Id == link.LinkId)
            .Select(item => item.Status)
            .SingleAsync();
        Assert.Equal(RequestUploadStatus.Active, status);
    }

    /// <summary>
    /// A Pending arrival is not a dead end. It writes no receipt, so the next
    /// submission of the same operation key reaches the command again, which
    /// asks custody what became of it instead of offering the bytes twice. A
    /// confirmation lands the identities, opens the fixed window, and only then
    /// tells the sender the document is retained.
    /// </summary>
    [Fact]
    public async Task APendingArrivalIsReconciledToConfirmedByTheNextArrivalWithTheSameKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        Assert.Contains(StoringMessage, first.CompletionBody, StringComparison.Ordinal);

        // Custody finishes storing it between the two submissions.
        custody.StatusDisposition = CaseArtifactCustodyDisposition.Confirmed;
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(RetainedMessage, second.CompletionBody, StringComparison.Ordinal);

        // Asked, not repeated.
        var call = Assert.Single(custody.Calls);
        Assert.Equal(1, custody.StatusCalls);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);
        Assert.Equal(Now, session.StartedAtUtc);
        Assert.Equal(Now.Add(PublicUploadSessionPolicy.Window), session.ExpiresAtUtc);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Equal(call.VersionId, occurrence.DocumentVersionId);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal($"box-file:{version.Id:N}", version.BoxFileId);
        Assert.Equal($"box-version:{version.Id:N}", version.BoxVersionId);

        // The confirmation earns the receipt the Pending deliberately did not.
        Assert.Single(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The other end of the same reconciliation: custody says it refused the
    /// file after all. The occurrence records the refusal, the window never
    /// opens, no receipt is written, and the sender is told to try again rather
    /// than that the document is held.
    /// </summary>
    [Fact]
    public async Task APendingArrivalIsReconciledToFailedByTheNextArrivalWithTheSameKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        custody.StatusDisposition = CaseArtifactCustodyDisposition.Failed;
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(RetryMessage, second.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(RetainedMessage, second.Body, StringComparison.Ordinal);
        Assert.Single(custody.Calls);
        Assert.Equal(1, custody.StatusCalls);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);
        Assert.Null(session.StartedAtUtc);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal("failed", occurrence.CustodyState);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Failed, version.CustodyStatus);
        Assert.Null(version.BoxFileId);

        Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());

        // The file the Pending consumed is not handed back at the moment of the
        // refusal (ASSUMPTION 6): the totals are recomputed from the accepted
        // occurrences the next time an arrival is accepted, and this refusal is
        // not one.
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// A hand-over that fails after custody has the bytes is recorded uncertain
    /// rather than left as it was offered, so the next submission asks about it
    /// instead of sending custody the same bytes a second time.
    /// </summary>
    [Fact]
    public async Task AThrownHandOverIsRecordedUnknownAndTheNextArrivalNeverRepeatsIt()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        custody.ThrowOnHandOver = new TimeoutException("the custody call timed out");
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(RetryMessage, first.Body, StringComparison.Ordinal);
        var call = Assert.Single(custody.Calls);
        Assert.Equal(Evidence.Length, call.ObservedContentLength);

        await using (var context = await CreateContextAsync(factory.Services))
        {
            var arrival = await context.Set<PublicUploadOccurrenceEntity>()
                .AsNoTracking()
                .SingleAsync();

            // Not the state it was offered from: that one would send the bytes
            // again on the retry.
            Assert.Equal("unknown", arrival.CustodyState);
            Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
                .AsNoTracking()
                .Where(item => item.RequestId == link.LinkId)
                .ToArrayAsync());
            Assert.Empty(await context.Set<CaseDocumentEntity>()
                .AsNoTracking()
                .Where(item => item.CaseId == link.CaseId)
                .ToArrayAsync());
            Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
        }

        // Custody would answer now. It is still never offered the bytes again:
        // an uncertain arrival is asked about, and this one named no document
        // to ask about, so it honestly stays uncertain.
        custody.ThrowOnHandOver = null;
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(RetryMessage, second.Body, StringComparison.Ordinal);
        Assert.Single(custody.Calls);
        Assert.Equal(0, custody.StatusCalls);

        await using var after = await CreateContextAsync(factory.Services);
        var occurrence = await after.Set<PublicUploadOccurrenceEntity>().AsNoTracking().SingleAsync();
        Assert.Equal("unknown", occurrence.CustodyState);
        Assert.Equal((0, 0L), await ReadLinkTotalsAsync(after, link.LinkId));
    }

    /// <summary>
    /// The custody rule refuses every authority that is not this exact link,
    /// and a refusal leaves nothing confirmed behind it.
    /// </summary>
    [Fact]
    public async Task CustodyRefusesEveryAuthorityThatIsNotThisExactActiveLink()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        await using var scope = factory.Services.CreateAsyncScope();
        var retention = scope.ServiceProvider.GetRequiredService<RetainIncomingArtifact>();
        var link = await SeedLinkAsync(factory.Services);
        var other = await SeedLinkAsync(factory.Services, reference: "PUBUP2");
        var revoked = await SeedLinkAsync(
            factory.Services,
            reference: "PUBUP3",
            status: RequestUploadStatus.Revoked,
            revokedAtUtc: Now);
        var expired = await SeedLinkAsync(
            factory.Services,
            reference: "PUBUP4",
            expiresAtUtc: Now.AddMinutes(-1));
        var inactive = await SeedLinkAsync(
            factory.Services,
            reference: "PUBUP5",
            status: RequestUploadStatus.Exhausted);

        // Staff rights are not this link's rights, however senior the actor.
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            link.CaseId,
            link.LinkId);

        // A request-link actor naming a different persisted link.
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.RequestLink(other.LinkId),
            link.CaseId,
            link.LinkId);

        // This link, but a Case that is not the one the link row records.
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.RequestLink(link.LinkId),
            other.CaseId,
            link.LinkId);

        // Holding — a null destination — is not open to a request link, and it
        // is refused for being holding rather than for failing validation.
        await Refuses<InvalidOperationException>(
            retention,
            ActionActor.RequestLink(link.LinkId),
            null,
            link.LinkId);

        // A link that no longer authorizes anything: revoked, expired, and one
        // that is simply not Active with no revocation to give it away.
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.RequestLink(revoked.LinkId),
            revoked.CaseId,
            revoked.LinkId);
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.RequestLink(expired.LinkId),
            expired.CaseId,
            expired.LinkId);
        await Refuses<StaffAuthorizationException>(
            retention,
            ActionActor.RequestLink(inactive.LinkId),
            inactive.CaseId,
            inactive.LinkId);

        // A refusal reaches no store, so there is no arrival and no document
        // against any of the five seeded Cases.
        Guid[] seeded =
            [link.CaseId, other.CaseId, revoked.CaseId, expired.CaseId, inactive.CaseId];
        await using var context = await CreateContextAsync(factory.Services);
        Assert.Empty(await context.Set<PublicUploadOccurrenceEntity>().AsNoTracking().ToArrayAsync());
        Assert.Empty(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => seeded.Contains(item.CaseId))
            .ToArrayAsync());
    }

    /// <summary>
    /// Without the retention command there is no custody to reach, so the
    /// submission path refuses before it writes anything at all. It must never
    /// fall back to recording an arrival it cannot retain.
    /// </summary>
    [Fact]
    public async Task WithoutTheRetentionCommandTheSubmissionRefusesAndWritesNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        var link = await SeedLinkAsync(factory.Services);

        var result = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        await using var context = await CreateContextAsync(factory.Services);
        Assert.Empty(await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .Where(item => item.RequestUploadLinkId == link.LinkId)
            .ToArrayAsync());
        Assert.Empty(await context.Set<PublicUploadOccurrenceEntity>().AsNoTracking().ToArrayAsync());
        Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Empty(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());
        Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The refusal is asserted by exact type, so an incidental validation
    /// failure inside the command cannot stand in for Stream A's authorization
    /// refusal.
    /// </summary>
    private static async Task Refuses<TException>(
        RetainIncomingArtifact retention,
        ActionActor actor,
        Guid? caseId,
        Guid linkId)
        where TException : Exception
    {
        await using var content = new MemoryStream(Evidence, writable: false);
        await Assert.ThrowsAsync<TException>(() => retention.ExecuteAsync(
            actor,
            new(
                Guid.NewGuid(),
                caseId,
                // A holding receipt only to satisfy the command's own "Case or
                // holding" invariant when the Case is deliberately absent.
                caseId is null ? Guid.NewGuid() : null,
                $"request:{linkId:N}:{Guid.NewGuid():N}",
                "evidence.txt",
                "text/plain",
                Evidence.Length,
                Sha256Hex(Evidence)),
            content,
            CancellationToken.None));
    }

    /// <summary>
    /// The registrations Stream A must add to the production host, standing in
    /// A04's adapter with the recording double.
    /// </summary>
    internal static WebApplicationFactory<Program> WithRetention(
        WebApplicationFactory<Program> baseFactory) =>
        baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<RecordingCaseArtifactCustody>();
            services.AddScoped<ICaseArtifactCustody>(provider =>
                provider.GetRequiredService<RecordingCaseArtifactCustody>());
            // Both ports, exactly as the production host must resolve them to
            // A04's adapter: without the status port a hand-over custody has
            // not finished could never be asked about.
            services.AddScoped<ICaseArtifactCustodyStatus>(provider =>
                provider.GetRequiredService<RecordingCaseArtifactCustody>());
            services.AddScoped<IIncomingArtifactRetentionStore, EfPublicUploadRetentionStore>();
            services.AddScoped<RetainIncomingArtifact>();
        }));

    private sealed record SeededLink(Guid CaseId, Guid LinkId, string Token);

    private static async Task<SeededLink> SeedLinkAsync(
        IServiceProvider services,
        string reference = "PUBUP1",
        RequestUploadStatus status = RequestUploadStatus.Active,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null)
    {
        await using var scope = services.CreateAsyncScope();
        var receiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(
            scope.ServiceProvider,
            $"{reference.ToLowerInvariant()}.pdf");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            scope.ServiceProvider,
            receiptId,
            reference,
            "Review");
        var issue = RequestUploadToken.Create();
        var linkId = Guid.NewGuid();
        await using var context = await CreateContextAsync(scope.ServiceProvider);
        context.Set<RequestUploadLinkEntity>().Add(new()
        {
            Id = linkId,
            CaseId = caseId,
            TokenDigest = issue.TokenDigest,
            Status = status,
            CreatedAtUtc = Now,
            // The policy only accepts a lifetime it would have issued itself,
            // so the expiry is the configured hour unless a test wants it past.
            ExpiresAtUtc = expiresAtUtc ?? Now.AddHours(1),
            RevokedAtUtc = revokedAtUtc,
            LimitsVersion = LimitsVersion,
            Version = 1,
            CreateOperationKey = $"request-create:{linkId:N}"
        });
        await context.SaveChangesAsync();
        return new(caseId, linkId, issue.Secret.Token);
    }

    private sealed record PostedUpload(
        HttpStatusCode StatusCode,
        string Body,
        string OperationKey,
        string CompletionBody);

    private static async Task<PostedUpload> PostEvidenceAsync(
        WebApplicationFactory<Program> factory,
        string token,
        string? operationKey = null)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var page = await client.GetAsync($"/Uploads/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var key = operationKey ?? FieldValue(html, "OperationKey");

        var file = new ByteArrayContent(Evidence);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(FieldValue(html, "__RequestVerificationToken")), "__RequestVerificationToken" },
            { new StringContent(token), "Token" },
            { new StringContent(key), "OperationKey" },
            { file, "Upload", "evidence.txt" }
        };
        using var response = await client.PostAsync($"/Uploads/{token}", form);
        var body = await response.Content.ReadAsStringAsync();

        // A completed submission redirects and says what happened on the page
        // it lands on, so the sentence the sender actually reads is only
        // visible after following the redirect with the same client.
        var completion = string.Empty;
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            using var completed = await client.GetAsync($"/Uploads/{token}");
            completion = await completed.Content.ReadAsStringAsync();
        }

        return new(response.StatusCode, body, key, completion);
    }

    private static string FieldValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render a '{name}' field.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The '{name}' field must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static async Task<(int FileCount, long ByteCount)> ReadLinkTotalsAsync(
        PegasusDbContext context,
        Guid linkId) =>
        await context.Set<RequestUploadLinkEntity>()
            .AsNoTracking()
            .Where(item => item.Id == linkId)
            .Select(item => ValueTuple.Create(item.AcceptedFileCount, item.AcceptedByteCount))
            .SingleAsync();

    private static Task<PegasusDbContext> CreateContextAsync(IServiceProvider services) =>
        services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

    internal static string Sha256Hex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();
}

/// <summary>
/// Stands in for Stream A's <c>EfCaseArtifactCustody</c> (A04) and enforces the
/// same authorization rule, so a caller that gets the authority wrong fails
/// here rather than passing a permissive fake.
/// </summary>
/// <remarks>
/// The rule, from Stream A: the actor must be
/// <see cref="ActionActor.RequestLink(Guid)"/> naming the persisted upload-link
/// row; the request's Case must be that link's own recorded Case; the link must
/// be re-read and found active, unrevoked and unexpired against the injected
/// clock; and holding — a null Case — is not open to a request-link actor. On
/// acceptance the adapter, not its caller, creates the document, the version
/// and the document occurrence, and returns their identities.
/// </remarks>
internal sealed class RecordingCaseArtifactCustody(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : ICaseArtifactCustody, ICaseArtifactCustodyStatus
{
    private readonly List<RecordedCustodyCall> calls = [];

    public CaseArtifactCustodyDisposition Disposition { get; set; } =
        CaseArtifactCustodyDisposition.Confirmed;

    /// <summary>
    /// What custody says when it is asked what became of a hand-over it has
    /// not finished. Unset means it says what it said at the hand-over.
    /// </summary>
    public CaseArtifactCustodyDisposition? StatusDisposition { get; set; }

    /// <summary>
    /// A fault raised after the bytes have been read — the shape of a timeout
    /// or a lost connection, where the caller cannot know what custody kept.
    /// </summary>
    public Exception? ThrowOnHandOver { get; set; }

    /// <summary>
    /// Whether the adapter creates the document occurrence a receipt's foreign
    /// key needs. A04 does; an adapter that does not is the branch that earns
    /// no receipt, and it must still be counted exactly once.
    /// </summary>
    public bool CreatesDocumentOccurrence { get; set; } = true;

    public int StatusCalls { get; private set; }

    public IReadOnlyList<RecordedCustodyCall> Calls
    {
        get
        {
            lock (calls)
            {
                return [.. calls];
            }
        }
    }

    public async Task<CaseArtifactCustodyResult> RetainAsync(
        CaseArtifactCustodyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var linkId = RequireAuthority(request, context, timeProvider.GetUtcNow());
        var caseId = request.CaseId!.Value;

        // The bytes are read once, here, so the recorded length and hash are
        // what custody actually received rather than what it was told.
        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var received = buffer.ToArray();

        var handOverState = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.OperationKey == request.OperationKey)
            .Select(item => item.CustodyState)
            .SingleOrDefaultAsync(cancellationToken);

        if (ThrowOnHandOver is { } fault)
        {
            // Custody has the bytes and then fails. The call is recorded first,
            // so a test can prove they were offered exactly once.
            lock (calls)
            {
                calls.Add(new(
                    request.Actor.Kind,
                    request.Actor.SubjectId,
                    linkId,
                    request.CaseId,
                    request.IntakeReceiptId,
                    request.OperationKey,
                    request.OccurrenceIdentity,
                    request.FileName,
                    received.Length,
                    PublicUploadRetentionWebTests.Sha256Hex(received),
                    handOverState,
                    null,
                    null,
                    null,
                    null));
            }

            throw fault;
        }

        var confirmed = Disposition == CaseArtifactCustodyDisposition.Confirmed;
        var accepted = confirmed || Disposition == CaseArtifactCustodyDisposition.Pending;
        Guid? documentId = null;
        Guid? versionId = null;
        string? boxFileId = null;
        string? boxVersionId = null;
        if (accepted)
        {
            var ordinal = checked((await context.Set<CaseDocumentEntity>()
                .Where(item => item.CaseId == caseId)
                .Select(item => (int?)item.Ordinal)
                .MaxAsync(cancellationToken) ?? 1) + 1);
            documentId = Guid.NewGuid();
            versionId = Guid.NewGuid();
            boxFileId = confirmed ? $"box-file:{versionId:N}" : null;
            boxVersionId = confirmed ? $"box-version:{versionId:N}" : null;
            context.Add(new CaseDocumentEntity
            {
                Id = documentId.Value,
                CaseId = caseId,
                Ordinal = ordinal,
                SourceOccurrenceIdentity = request.OccurrenceIdentity
            });
            context.Add(new DocumentVersionEntity
            {
                Id = versionId.Value,
                DocumentId = documentId.Value,
                Version = 1,
                FileName = request.FileName,
                MediaType = request.MediaType,
                ContentLength = received.Length,
                Sha256 = request.Sha256,
                // Pending until custody confirms. The caller never writes this.
                CustodyStatus = confirmed
                    ? DocumentCustodyStatus.Confirmed
                    : DocumentCustodyStatus.Pending,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                CreatedBy = "request-upload",
                IsCurrent = true
            });
            if (CreatesDocumentOccurrence)
            {
                context.Add(new DocumentOccurrenceEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    DocumentId = documentId.Value,
                    VersionId = versionId.Value,
                    Ordinal = ordinal,
                    SemanticRole = DocumentSemanticRole.Other,
                    Source = DocumentSource.RequestUpload,
                    SourceOccurrenceIdentity = request.OccurrenceIdentity,
                    RecordedAtUtc = timeProvider.GetUtcNow(),
                    OperationKey = request.OperationKey
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        lock (calls)
        {
            calls.Add(new(
                request.Actor.Kind,
                request.Actor.SubjectId,
                linkId,
                request.CaseId,
                request.IntakeReceiptId,
                request.OperationKey,
                request.OccurrenceIdentity,
                request.FileName,
                received.Length,
                PublicUploadRetentionWebTests.Sha256Hex(received),
                handOverState,
                documentId,
                versionId,
                boxFileId,
                boxVersionId));
        }

        return new(
            Disposition,
            documentId,
            versionId,
            boxFileId,
            boxVersionId,
            request.Sha256,
            received.Length,
            request.MediaType,
            accepted ? null : $"custody-{Disposition}".ToLowerInvariant(),
            PendingContentStorageKey: null);
    }

    /// <summary>
    /// What custody durably holds for one exact version. A confirmation is the
    /// adapter's own write — it moves the version out of Pending and records
    /// the remote identities — because in production the caller only ever
    /// learns about it by asking.
    /// </summary>
    public async Task<CaseArtifactCustodyResult> GetAsync(
        ActionActor actor,
        Guid caseId,
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        StatusCalls++;
        var disposition = StatusDisposition ?? Disposition;
        var confirmed = disposition == CaseArtifactCustodyDisposition.Confirmed;
        var boxFileId = confirmed ? $"box-file:{versionId:N}" : null;
        var boxVersionId = confirmed ? $"box-version:{versionId:N}" : null;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var version = await context.Set<DocumentVersionEntity>()
            .SingleAsync(item => item.Id == versionId, cancellationToken);
        if (confirmed)
        {
            version.CustodyStatus = DocumentCustodyStatus.Confirmed;
            version.BoxFileId = boxFileId;
            version.BoxVersionId = boxVersionId;
        }
        else if (disposition == CaseArtifactCustodyDisposition.Failed)
        {
            version.CustodyStatus = DocumentCustodyStatus.Failed;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(
            disposition,
            documentId,
            versionId,
            boxFileId,
            boxVersionId,
            version.Sha256,
            version.ContentLength,
            version.MediaType,
            disposition == CaseArtifactCustodyDisposition.Failed ? "custody-failed" : null,
            PendingContentStorageKey: null);
    }

    private static Guid RequireAuthority(
        CaseArtifactCustodyRequest request,
        PegasusDbContext context,
        DateTimeOffset nowUtc)
    {
        if (request.Actor.Kind != ActorKind.RequestLink)
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        }
        if (!Guid.TryParseExact(request.Actor.SubjectId, "D", out var linkId))
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        }
        if (request.CaseId is not { } caseId)
        {
            throw new InvalidOperationException(
                "A request-link actor cannot retain into holding.");
        }

        var link = context.Set<RequestUploadLinkEntity>()
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == linkId)
            ?? throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        if (link.CaseId != caseId
            || link.Status != RequestUploadStatus.Active
            || link.RevokedAtUtc is not null
            || link.ExpiresAtUtc <= nowUtc)
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        }

        return linkId;
    }
}

/// <summary>What one hand-over actually presented to custody.</summary>
internal sealed record RecordedCustodyCall(
    ActorKind ActorKind,
    string ActorSubjectId,
    Guid LinkId,
    Guid? CaseId,
    Guid? IntakeReceiptId,
    string OperationKey,
    string OccurrenceIdentity,
    string FileName,
    long ObservedContentLength,
    string ObservedSha256,
    string? CustodyStateAtHandOver,
    Guid? DocumentId,
    Guid? VersionId,
    string? BoxFileId,
    string? BoxVersionId);
