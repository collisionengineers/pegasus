using System.Collections.Concurrent;
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
/// stands in for it and <em>enforces A's authorization rules rather than
/// assuming them</em>: it re-reads the upload-link row and refuses anything but
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

    /// <summary>
    /// A second, different file. Deliberately a different length as well as a
    /// different digest, so a slot that took the wrong one is visible in the
    /// totals and not only in a hash.
    /// </summary>
    private static readonly byte[] OtherEvidence =
        "a second and entirely different public upload"u8.ToArray();

    // The three sentences the public page can end on. They are asserted
    // verbatim because which one is shown is the whole of what the sender is
    // told about custody.
    private const string RetainedMessage =
        "Your document was received and retained securely.";

    private const string StoringMessage =
        "Your document was received and is being stored.";

    private const string RetryMessage =
        "The document could not be retained. Try again using the same upload operation.";

    private const string RefusedMessage =
        "This document was not accepted. Reload the link and try again.";

    private const string ConflictMessage =
        "This upload operation was already used for different content.";

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

        // The arrival was durable before custody was asked, and by then it was
        // claimed: the conditional update out of "arrived" had already
        // committed, which is what stops a second caller of this key offering
        // the same bytes and what stops a lost result reopening the hand-over.
        // Not a Pending custody has not given, and certainly not a Confirmed
        // one.
        Assert.Equal("unknown", call.CustodyStateAtHandOver);

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
    /// A Pending arrival is not a dead end, and it is never re-offered either.
    /// The next submission of the same operation key reaches the command
    /// again, which asks custody what became of it instead of sending the
    /// bytes twice - and the sender may make that read: Stream A's fence for
    /// both status reads is the one the hand-over already passed, this exact
    /// active link naming its own Case (PR 673 comments 5560737585 and
    /// 5561151076). So a sender recovers its own submission, and the
    /// confirmation lands on the arrival and the version the first hand-over
    /// created rather than on a second copy of the bytes.
    /// </summary>
    [Fact]
    public async Task APendingArrivalIsReconciledByItsOwnSenderAndNeverReOffered()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        Assert.Contains(StoringMessage, first.CompletionBody, StringComparison.Ordinal);

        // Custody finishes storing it between the two submissions, which is
        // exactly what the sender's own retry now finds out.
        custody.StatusDisposition = CaseArtifactCustodyDisposition.Confirmed;
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(RetainedMessage, second.CompletionBody, StringComparison.Ordinal);

        // Asked, not repeated: the bytes were offered once, one intent was
        // ever initiated, and the answer came from the precise read because
        // the arrival named a document to ask about.
        Assert.Single(custody.Calls);
        Assert.Equal(1, custody.ProviderInitiations);
        Assert.Equal(1, custody.StatusCalls);
        Assert.Equal(0, custody.LookupCalls);

        await using var context = await CreateContextAsync(factory.Services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestUploadLinkId == link.LinkId);

        // The window opens on the confirmation, not on the Pending that
        // preceded it.
        Assert.Equal(Now, session.StartedAtUtc);
        Assert.Equal(Now.Add(PublicUploadSessionPolicy.Window), session.ExpiresAtUtc);

        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        Assert.Equal("confirmed", occurrence.CustodyState);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal($"box-file:{version.Id:N}", version.BoxFileId);

        // The authority the bytes arrived under, which is the link and never a
        // member of staff.
        Assert.Equal($"RequestLink:{link.LinkId:D}", version.CreatedBy);

        // A confirmed submission earns its receipt, and one document holds it.
        Assert.Single(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Single(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());

        // One occurrence, counted once, however many times it is submitted.
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The reconciliation the public sender may not perform, performed by an
    /// authority that may. Custody is asked under the same operation key —
    /// never offered the bytes again — and a confirmation lands the identities
    /// on the version the hand-over created.
    /// </summary>
    [Fact]
    public async Task AStaffReconciliationConfirmsAPendingArrivalWithoutASecondHandOver()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        Assert.Contains(StoringMessage, first.CompletionBody, StringComparison.Ordinal);
        custody.StatusDisposition = CaseArtifactCustodyDisposition.Confirmed;

        var reconciled = await ReconcileAsStaffAsync(factory, link, first.OperationKey);

        Assert.Equal(IncomingArtifactCustodyState.Confirmed, reconciled.State);
        var call = Assert.Single(custody.Calls);
        Assert.Equal(1, custody.StatusCalls);

        await using var context = await CreateContextAsync(factory.Services);
        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Equal(call.VersionId, occurrence.DocumentVersionId);

        var version = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal($"box-file:{version.Id:N}", version.BoxFileId);
        Assert.Equal($"box-version:{version.Id:N}", version.BoxVersionId);

        // The receipt and the window belong to the accept path, which a
        // reconciliation does not re-enter. Recorded as part of the same
        // handoff: the sender's own retry is what would earn them, and the
        // sender cannot make the status read that would let it.
        Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
            .AsNoTracking()
            .Where(item => item.RequestId == link.LinkId)
            .ToArrayAsync());
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// The other end of the same reconciliation: custody says it refused the
    /// file after all. The occurrence and the version record the refusal, and
    /// nothing anywhere says the document is held.
    /// </summary>
    [Fact]
    public async Task AStaffReconciliationRecordsCustodysRefusalOfAPendingArrival()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        custody.StatusDisposition = CaseArtifactCustodyDisposition.Failed;

        var reconciled = await ReconcileAsStaffAsync(factory, link, first.OperationKey);

        Assert.Equal(IncomingArtifactCustodyState.Failed, reconciled.State);
        Assert.Null(reconciled.BoxFileId);
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
    /// before it does anything else. Custody owns up to nothing - this is the
    /// crash-before-custody shape, where the claim was taken and no intent
    /// exists - so the same bytes go under the same key, which is the only
    /// thing that ever resolves such a claim and is what custody converges on
    /// one intent (Stream A, PR 673 comment 5561151076).
    /// </summary>
    [Fact]
    public async Task AThrownHandOverIsAskedAboutAndThenReOfferedUnderTheSameKeyOnce()
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

        // The page is still asking for the same key, so the retry is the same
        // submission and not a second one.
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        custody.ThrowOnHandOver = null;
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(RetainedMessage, second.CompletionBody, StringComparison.Ordinal);

        // Asked first - it named no document, so by the operation key it was
        // accepted under - and only then offered again, under that same key.
        Assert.Equal(1, custody.LookupCalls);
        Assert.Equal(0, custody.StatusCalls);
        Assert.Equal(2, custody.HandOverAttempts);
        Assert.Equal(
            [
                $"request:{link.LinkId:N}:{first.OperationKey}",
                $"request:{link.LinkId:N}:{first.OperationKey}"
            ],
            custody.Calls.Select(item => item.OperationKey));

        // One durable intent and one initiation, which is the invariant - not
        // one invocation.
        Assert.Equal(1, custody.ProviderInitiations);

        await using var after = await CreateContextAsync(factory.Services);
        var occurrence = await after.Set<PublicUploadOccurrenceEntity>().AsNoTracking().SingleAsync();
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Single(await after.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(after, link.LinkId));
    }

    /// <summary>
    /// Two submissions of one operation key arriving at once, with the first
    /// call still in flight. The claim decides which of them offers the bytes
    /// first; the other asks by the key, observes nothing committed - which is
    /// exactly what a call still inside custody looks like - and may then
    /// offer the same bytes under that same key. What must come of it is one
    /// durable intent and one initiating write, and custody's serialized
    /// same-key path is what makes that so rather than the caller's timing
    /// (Stream A, PR 673 comment 5561151076).
    /// </summary>
    [Fact]
    public async Task TwoSimultaneousSubmissionsOfOneOperationKeyConvergeOnOneIntent()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        custody.HoldHandOver = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var link = await SeedLinkAsync(factory.Services);
        var key = await ReadOperationKeyAsync(factory, link.Token);

        // The first caller is parked inside custody with the claim taken and
        // nothing committed.
        var first = PostEvidenceAsync(factory, link.Token, key);
        await custody.HandOverEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // The claim is not reopened and the page keeps presenting the same
        // key, so the retry is this submission again rather than a second
        // deliberate one.
        Assert.Equal("unknown", await ReadOccurrenceStateAsync(factory.Services, link.LinkId));
        Assert.Equal(key, await ReadOperationKeyAsync(factory, link.Token));

        var retry = PostEvidenceAsync(factory, link.Token, key);

        // Waited for rather than assumed: what is proved is convergence, not a
        // race the test happened to win.
        await WaitUntilAsync(
            () => Volatile.Read(ref custody.HandOverAttempts) == 2,
            "the retry to reach custody under the same key");
        Assert.Equal(1, custody.LookupCalls);
        Assert.Equal(0, custody.StatusCalls);

        custody.HoldHandOver.SetResult();
        var confirmed = await first;
        var second = await retry;

        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Contains(RetainedMessage, confirmed.CompletionBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);

        // Two invocations of one key, one intent, one initiating write.
        Assert.Equal(2, custody.HandOverAttempts);
        Assert.Equal(1, custody.ProviderInitiations);

        await using var context = await CreateContextAsync(factory.Services);
        var occurrence = Assert.Single(await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .ToArrayAsync());
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Single(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// Plan item 6's additions, under the key rule that protects them. While
    /// the first file's arrival is unresolved the page keeps presenting its
    /// key: the same bytes sent under it are that submission again and
    /// reconcile, and a different file sent under it is the second deliberate
    /// submission item 6 allows - so it gets its own server-issued key rather
    /// than the refusal a resolved key would give it. The first key never
    /// comes to name the second file, and the second file's own retry is a
    /// retry rather than a third submission, because its key is derived from
    /// its bytes. That is not a link-and-hash identity standing in for the
    /// intent (Stream A's caveat, PR 673 comment 5560737585): the root key is
    /// still the identity, and the digest only tells one file from another
    /// under it.
    /// </summary>
    [Fact]
    public async Task ASecondDifferentFileUnderAnUnresolvedKeyBecomesItsOwnSubmission()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();

        // Pending, so the first submission stays unresolved throughout.
        custody.Disposition = CaseArtifactCustodyDisposition.Pending;
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);
        Assert.Contains(StoringMessage, first.CompletionBody, StringComparison.Ordinal);
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        // The same bytes under the re-presented key: the same submission,
        // reconciled and never offered again.
        var again = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Contains(StoringMessage, again.CompletionBody, StringComparison.Ordinal);
        Assert.Equal(1, custody.HandOverAttempts);
        Assert.Equal(1, custody.ProviderInitiations);
        Assert.Equal(1, custody.StatusCalls);

        // A different file under that same key. It is not a replacement and
        // not a conflict: it is a submission of its own.
        var second = await PostEvidenceAsync(
            factory,
            link.Token,
            first.OperationKey,
            OtherEvidence,
            "estimate.txt");

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(StoringMessage, second.CompletionBody, StringComparison.Ordinal);
        Assert.Equal(2, custody.HandOverAttempts);
        Assert.Equal(2, custody.ProviderInitiations);

        // And its own retry is a retry, because its key is derived from its
        // bytes rather than minted afresh.
        var secondAgain = await PostEvidenceAsync(
            factory,
            link.Token,
            first.OperationKey,
            OtherEvidence,
            "estimate.txt");

        Assert.Equal(HttpStatusCode.Redirect, secondAgain.StatusCode);
        Assert.Equal(2, custody.HandOverAttempts);
        Assert.Equal(2, custody.ProviderInitiations);

        // The page still presents the first submission's key: it is the one
        // that is still unresolved, and it was never replaced.
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        await using var context = await CreateContextAsync(factory.Services);
        var arrivals = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.OperationKey.StartsWith($"request:{link.LinkId:N}:"))
            .OrderBy(item => item.OperationKey)
            .ToArrayAsync();

        Assert.Equal(2, arrivals.Length);

        // The sender's own key, still naming the first file and its bytes.
        Assert.Equal($"request:{link.LinkId:N}:{first.OperationKey}", arrivals[0].OperationKey);
        Assert.Equal(Sha256Hex(Evidence), arrivals[0].Sha256);
        Assert.Equal(Evidence.Length, arrivals[0].Size);

        // The second file's own key: this root, and these bytes.
        Assert.Equal(
            $"request:{link.LinkId:N}:{first.OperationKey}~{Sha256Hex(OtherEvidence)}",
            arrivals[1].OperationKey);
        Assert.Equal(Sha256Hex(OtherEvidence), arrivals[1].Sha256);
        Assert.Equal(OtherEvidence.Length, arrivals[1].Size);

        // Two files accepted, two documents, and the session's totals counting
        // each exactly once however many times either was sent.
        Assert.Equal(2, (await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync()).Length);
        Assert.Equal(
            (2, (long)(Evidence.Length + OtherEvidence.Length)),
            await ReadLinkTotalsAsync(context, link.LinkId));
    }

    /// <summary>
    /// Custody accepted, and the answer could not be written down. The claim
    /// taken before the call is what makes that survivable: the arrival stays
    /// claimed rather than offerable, the retry asks under the original key,
    /// and the identities custody committed to come back rather than being
    /// created a second time.
    /// </summary>
    [Fact]
    public async Task ARecordThatFailsAfterCustodyAcceptedIsRecoveredByTheOriginalKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        factory.Services.GetRequiredService<RetentionRecordingFault>().Arm();
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(RetryMessage, first.Body, StringComparison.Ordinal);
        var call = Assert.Single(custody.Calls);
        Assert.NotNull(call.VersionId);

        await using (var context = await CreateContextAsync(factory.Services))
        {
            var arrival = await context.Set<PublicUploadOccurrenceEntity>()
                .AsNoTracking()
                .SingleAsync();

            // Claimed, and nothing more: custody's answer never reached it.
            Assert.Equal("unknown", arrival.CustodyState);
            Assert.Null(arrival.DocumentVersionId);
            Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
        }

        // The page is still asking for the same operation key, so the sender's
        // retry is the same submission.
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains(RetainedMessage, second.CompletionBody, StringComparison.Ordinal);

        // Asked, not repeated: one hand-over, one initiation, and the answer
        // recovered by the key it was accepted under rather than re-offered.
        Assert.Equal(1, custody.HandOverAttempts);
        Assert.Equal(1, custody.ProviderInitiations);
        Assert.Equal(1, custody.LookupCalls);
        Assert.Equal(0, custody.StatusCalls);

        await using var after = await CreateContextAsync(factory.Services);
        var occurrence = await after.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("confirmed", occurrence.CustodyState);
        Assert.Equal(call.DocumentId, occurrence.DocumentId);
        Assert.Equal(call.VersionId, occurrence.DocumentVersionId);

        var version = await after.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == occurrence.DocumentVersionId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal($"box-file:{version.Id:N}", version.BoxFileId);

        // One file, once, and one document version - not a second copy of the
        // bytes custody already held.
        Assert.Single(await after.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());
        Assert.Equal((1, (long)Evidence.Length), await ReadLinkTotalsAsync(after, link.LinkId));
    }

    /// <summary>
    /// Custody declining the authority is a refusal of that attempted
    /// acceptance, whatever it had read by then. The arrival is closed as
    /// refused rather than left uncertain, the sender is told so in a sentence
    /// that discloses nothing, and only then does the page issue a new
    /// operation key - because only then is a further submission a new
    /// deliberate one rather than a duplicate.
    /// </summary>
    [Fact]
    public async Task ARefusedHandOverIsRecordedFailedAndTheNextLoadIssuesANewKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.ThrowOnHandOver =
            new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        var link = await SeedLinkAsync(factory.Services);

        var refused = await PostEvidenceAsync(factory, link.Token);

        // A plain sentence on a public page, not the 500 an unhandled
        // authorization fault would be, and not "try the same operation again".
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(RefusedMessage, refused.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(RetryMessage, refused.Body, StringComparison.Ordinal);
        Assert.Equal(1, custody.HandOverAttempts);

        Assert.Equal("failed", await ReadOccurrenceStateAsync(factory.Services, link.LinkId));

        await using (var context = await CreateContextAsync(factory.Services))
        {
            Assert.Empty(await context.Set<RequestUploadReceiptEntity>()
                .AsNoTracking()
                .Where(item => item.RequestId == link.LinkId)
                .ToArrayAsync());
            Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
        }

        // A key custody has answered is closed. A different file sent under
        // it is the conflict it always was - only an unresolved key admits a
        // second submission - and nothing is offered for it.
        var conflicting = await PostEvidenceAsync(
            factory,
            link.Token,
            refused.OperationKey,
            OtherEvidence,
            "estimate.txt");

        Assert.Equal(HttpStatusCode.OK, conflicting.StatusCode);
        Assert.Contains(ConflictMessage, conflicting.Body, StringComparison.Ordinal);
        Assert.Equal(1, custody.HandOverAttempts);

        // Nothing is outstanding any more, so the next submission is a new one.
        Assert.NotEqual(refused.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        custody.ThrowOnHandOver = null;
        custody.Disposition = CaseArtifactCustodyDisposition.Confirmed;
        var accepted = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.NotEqual(refused.OperationKey, accepted.OperationKey);
        Assert.Equal(2, custody.HandOverAttempts);
    }

    /// <summary>
    /// An <see cref="ArgumentException"/> out of the adapter is not a refusal.
    /// An adapter can raise one after it has committed as easily as before, so
    /// the arrival stays uncertain, the key stays the sender's, and the retry
    /// asks rather than offering the bytes a second time.
    /// </summary>
    [Fact]
    public async Task AnAdapterArgumentExceptionLeavesTheArrivalUncertainAndTheKeyUnchanged()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        var custody = factory.Services.GetRequiredService<RecordingCaseArtifactCustody>();
        custody.ThrowOnHandOver = new ArgumentException("the adapter did not like the request");
        var link = await SeedLinkAsync(factory.Services);

        var first = await PostEvidenceAsync(factory, link.Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(RetryMessage, first.Body, StringComparison.Ordinal);
        Assert.Equal("unknown", await ReadOccurrenceStateAsync(factory.Services, link.LinkId));
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        // The adapter keeps raising it, so the retry ends where the first
        // attempt did: asked about, offered again under the same key, and
        // still uncertain rather than closed.
        var second = await PostEvidenceAsync(factory, link.Token, first.OperationKey);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(RetryMessage, second.Body, StringComparison.Ordinal);
        Assert.Equal(2, custody.HandOverAttempts);
        Assert.Equal(1, custody.LookupCalls);
        Assert.Equal(0, custody.ProviderInitiations);
        Assert.Equal("unknown", await ReadOccurrenceStateAsync(factory.Services, link.LinkId));
        Assert.Equal(first.OperationKey, await ReadOperationKeyAsync(factory, link.Token));

        await using var context = await CreateContextAsync(factory.Services);
        Assert.Empty(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == link.CaseId)
            .ToArrayAsync());
        Assert.Equal((0, 0L), await ReadLinkTotalsAsync(context, link.LinkId));
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
            factory.Services,
            retention,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            link.CaseId,
            link.LinkId);

        // A request-link actor naming a different persisted link.
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(other.LinkId),
            link.CaseId,
            link.LinkId);

        // This link, but a Case that is not the one the link row records.
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(link.LinkId),
            other.CaseId,
            link.LinkId);

        // Holding — a null destination — is not open to a request link. The
        // fake receipt injected by the helper gets it past the command's own
        // validation, so what refuses it is custody's rule and nothing else.
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(link.LinkId),
            null,
            link.LinkId);

        // A link that no longer authorizes anything: revoked, expired, and one
        // that is simply not Active with no revocation to give it away.
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(revoked.LinkId),
            revoked.CaseId,
            revoked.LinkId);
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(expired.LinkId),
            expired.CaseId,
            expired.LinkId);
        await Refuses<StaffAuthorizationException>(
            factory.Services,
            retention,
            ActionActor.RequestLink(inactive.LinkId),
            inactive.CaseId,
            inactive.LinkId);

        // Both status reads carry the hand-over's own fence, so an authority
        // that may not hand bytes over through this link may not ask what
        // became of them either - whichever of the two reads it reaches for.
        // A sender reading its own submission is proved where there is one to
        // read: APendingArrivalIsReconciledByItsOwnSenderAndNeverReOffered.
        var status = scope.ServiceProvider.GetRequiredService<ICaseArtifactCustodyStatus>();
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            status.GetAsync(
                ActionActor.RequestLink(other.LinkId),
                link.CaseId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            status.GetAsync(
                ActionActor.RequestLink(revoked.LinkId),
                revoked.CaseId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            status.FindByOperationKeyAsync(
                ActionActor.RequestLink(other.LinkId),
                link.CaseId,
                $"request:{link.LinkId:N}:{Guid.NewGuid():N}",
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            status.FindByOperationKeyAsync(
                ActionActor.RequestLink(revoked.LinkId),
                revoked.CaseId,
                $"request:{revoked.LinkId:N}:{Guid.NewGuid():N}",
                CancellationToken.None));

        // The link's own read is admitted and observes nothing, which is not
        // permission to start anything: it is the absence a re-offer under the
        // same key is the answer to.
        Assert.Null(await status.FindByOperationKeyAsync(
            ActionActor.RequestLink(link.LinkId),
            link.CaseId,
            $"request:{link.LinkId:N}:{Guid.NewGuid():N}",
            CancellationToken.None));

        // A refusal retains nothing against any of the five seeded Cases, and
        // every arrival it was attempted from is closed as the refusal it got
        // rather than left uncertain for someone to reconcile.
        Guid[] seeded =
            [link.CaseId, other.CaseId, revoked.CaseId, expired.CaseId, inactive.CaseId];
        await using var context = await CreateContextAsync(factory.Services);
        var refusedArrivals = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .ToArrayAsync();
        Assert.Equal(7, refusedArrivals.Length);
        Assert.All(refusedArrivals, item => Assert.Equal("failed", item.CustodyState));
        Assert.Empty(await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => seeded.Contains(item.CaseId))
            .ToArrayAsync());
    }

    [Fact]
    public async Task AnActiveLinkCannotReadAnotherActiveLinksAcceptedVersionOnTheSameCase()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithRetention(baseFactory);
        factory.Services.GetRequiredService<RecordingCaseArtifactCustody>().Disposition =
            CaseArtifactCustodyDisposition.Confirmed;
        var owner = await SeedLinkAsync(factory.Services, reference: "PUBUP-FENCE");
        var accepted = await PostEvidenceAsync(factory, owner.Token);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var scopedOperationKey = EfPublicUploadRetentionStore.ScopeOperationKey(
            owner.LinkId,
            accepted.OperationKey);

        Guid documentId;
        Guid versionId;
        var foreignLinkId = Guid.NewGuid();
        await using (var context = await CreateContextAsync(factory.Services))
        {
            var occurrence = await context.Set<DocumentOccurrenceEntity>()
                .AsNoTracking()
                .SingleAsync(item => item.OperationKey == scopedOperationKey);
            documentId = occurrence.DocumentId;
            versionId = occurrence.VersionId;
            context.Set<RequestUploadLinkEntity>().Add(new()
            {
                Id = foreignLinkId,
                CaseId = owner.CaseId,
                TokenDigest = RequestUploadToken.Create().TokenDigest,
                Status = RequestUploadStatus.Active,
                CreatedAtUtc = Now,
                ExpiresAtUtc = Now.AddHours(1),
                LimitsVersion = LimitsVersion,
                Version = 1,
                CreateOperationKey = $"request-create:{foreignLinkId:N}"
            });
            await context.SaveChangesAsync();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var status = scope.ServiceProvider.GetRequiredService<ICaseArtifactCustodyStatus>();
        var foreignActor = ActionActor.RequestLink(foreignLinkId);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => status.GetAsync(
            foreignActor, owner.CaseId, documentId, versionId, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => status.FindByOperationKeyAsync(
            foreignActor, owner.CaseId, scopedOperationKey, CancellationToken.None));

        var ownResult = await status.GetAsync(
            ActionActor.RequestLink(owner.LinkId),
            owner.CaseId,
            documentId,
            versionId,
            CancellationToken.None);
        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, ownResult.Disposition);
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
    /// Asks custody what became of an arrival under an authority that may make
    /// the status read. Same operation key, so the command reconciles rather
    /// than offering the bytes a second time; the occurrence identity and the
    /// content are only there to satisfy the command's own validation, and
    /// neither is reached.
    /// </summary>
    private static async Task<RetainedIncomingArtifact> ReconcileAsStaffAsync(
        WebApplicationFactory<Program> factory,
        SeededLink link,
        string senderOperationKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var content = new MemoryStream(Evidence, writable: false);
        return await scope.ServiceProvider.GetRequiredService<RetainIncomingArtifact>()
            .ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                new(
                    Guid.NewGuid(),
                    link.CaseId,
                    null,
                    $"request:{link.LinkId:N}:{senderOperationKey}",
                    "evidence.txt",
                    "text/plain",
                    Evidence.Length,
                    Sha256Hex(Evidence)),
                content,
                CancellationToken.None);
    }

    /// <summary>
    /// The refusal is asserted by exact type, so an incidental validation
    /// failure inside the command cannot stand in for Stream A's authorization
    /// refusal - and the arrival is staged first, because the command offers
    /// nothing its store has not committed and a refusal reached any other way
    /// would prove nothing about custody's rule.
    /// </summary>
    private static async Task Refuses<TException>(
        IServiceProvider services,
        RetainIncomingArtifact retention,
        ActionActor actor,
        Guid? caseId,
        Guid linkId)
        where TException : Exception
    {
        var occurrence = await StageArrivalAsync(services, linkId, caseId);
        await using var content = new MemoryStream(Evidence, writable: false);
        await Assert.ThrowsAsync<TException>(() => retention.ExecuteAsync(
            actor,
            occurrence,
            content,
            CancellationToken.None));
    }

    /// <summary>
    /// Commits one arrival for a link the way the submission path commits it
    /// before any hand-over, and returns the occurrence a caller would then
    /// offer. One session per link, reused, because that pair is uniquely
    /// indexed.
    /// </summary>
    private static async Task<IncomingArtifactOccurrence> StageArrivalAsync(
        IServiceProvider services,
        Guid linkId,
        Guid? caseId)
    {
        await using var context = await CreateContextAsync(services);
        var session = await context.Set<PublicUploadSessionEntity>()
            .SingleOrDefaultAsync(item => item.RequestUploadLinkId == linkId);
        if (session is null)
        {
            session = new()
            {
                Id = Guid.NewGuid(),
                RequestUploadLinkId = linkId,
                LimitsVersion = LimitsVersion,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
            context.Add(session);
        }

        var occurrenceId = Guid.NewGuid();
        var operationKey = $"request:{linkId:N}:{Guid.NewGuid():N}";
        context.Add(new PublicUploadOccurrenceEntity
        {
            Id = occurrenceId,
            SessionId = session.Id,
            OperationKey = operationKey,
            ProposedName = "evidence.txt",
            MediaType = "text/plain",
            Size = Evidence.Length,
            Sha256 = Sha256Hex(Evidence),
            CustodyState = EfPublicUploadRetentionStore.ArrivedCode
        });
        await context.SaveChangesAsync();
        return new(
            occurrenceId,
            caseId,
            // A holding receipt only to satisfy the command's own "Case or
            // holding" invariant when the Case is deliberately absent.
            caseId is null ? Guid.NewGuid() : null,
            operationKey,
            "evidence.txt",
            "text/plain",
            Evidence.Length,
            Sha256Hex(Evidence));
    }

    /// <summary>
    /// Waits for something another request is doing, so a proof about two
    /// callers converging is not a race this test happened to win.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"Timed out waiting for {what}.");
            await Task.Delay(10);
        }
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
            // The production store, reached through a decorator that is inert
            // until a test arms it. Recording the result of a hand-over
            // custody has already answered is the one failure a caller cannot
            // provoke from outside, and it is exactly the one the claim exists
            // to survive.
            services.AddSingleton<RetentionRecordingFault>();
            services.AddScoped<IIncomingArtifactRetentionStore, FaultInjectingRetentionStore>();
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
        string? operationKey = null,
        byte[]? content = null,
        string fileName = "evidence.txt")
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var page = await client.GetAsync($"/Uploads/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var key = operationKey ?? FieldValue(html, "OperationKey");

        var file = new ByteArrayContent(content ?? Evidence);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(FieldValue(html, "__RequestVerificationToken")), "__RequestVerificationToken" },
            { new StringContent(token), "Token" },
            { new StringContent(key), "OperationKey" },
            { file, "Upload", fileName }
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

    /// <summary>
    /// The operation key the page is currently handing senders. It is the
    /// whole of whether a retry reconciles an outstanding submission or
    /// becomes a second one.
    /// </summary>
    private static async Task<string> ReadOperationKeyAsync(
        WebApplicationFactory<Program> factory,
        string token)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var page = await client.GetAsync($"/Uploads/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return FieldValue(await page.Content.ReadAsStringAsync(), "OperationKey");
    }

    private static async Task<string> ReadOccurrenceStateAsync(
        IServiceProvider services,
        Guid linkId)
    {
        await using var context = await CreateContextAsync(services);
        return await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.OperationKey.StartsWith($"request:{linkId:N}:"))
            .Select(item => item.CustodyState)
            .SingleAsync();
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
/// <para>
/// The rule, from Stream A: the actor must be
/// <see cref="ActionActor.RequestLink(Guid)"/> naming the persisted upload-link
/// row; the request's Case must be that link's own recorded Case; the link must
/// be re-read and found active, unrevoked and unexpired against the injected
/// clock; and holding — a null Case — is not open to a request-link actor. On
/// acceptance the adapter, not its caller, creates the document, the version
/// and the document occurrence, and returns their identities.
/// </para>
/// <para>
/// Both status reads carry the same fence as the hand-over, which is Stream
/// A's published rule for them (PR 673 comments 5560737585 and 5561151076):
/// staff casework, or the exact persisted upload link naming its own Case
/// while that link is active, unrevoked and unexpired. A sender may therefore
/// find out what became of its own submission, and only its own: another
/// link, another Case or a revoked link is refused from either read.
/// </para>
/// <para>
/// It also converges a repeated operation key the way A's adapter does. The
/// intent is written inside the accepting transaction and
/// <c>DocumentOccurrences</c> is uniquely indexed on Case and operation key,
/// so a second call under one key returns the intent the first committed and
/// initiates no storage of its own; <see cref="ProviderInitiations"/> counts
/// the calls that did initiate it. Same-key calls are serialized here as A's
/// serializable request-link path serializes them, which is what makes a
/// delayed first call and its retry converge on one intent rather than two.
/// One thing it does not model: with
/// <see cref="CreatesDocumentOccurrence"/> off there is no intent row to
/// converge on, so that flag belongs only to tests where nothing is
/// re-offered.
/// </para>
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

    /// <summary>
    /// How many calls actually created the durable intent and initiated
    /// storage. A same-key call that finds the intent already committed adds
    /// nothing to it, so this is the count the one-durable-intent invariant is
    /// about - <see cref="HandOverAttempts"/> and <see cref="Calls"/> count
    /// invocations, which A's revised rule no longer bounds at one.
    /// </summary>
    public int ProviderInitiations;

    /// <summary>
    /// One gate per operation key, standing in for the serializable
    /// request-link path A's adapter commits its intent inside. Without it two
    /// same-key calls could both find no intent and both create one, which is
    /// the database's job to prevent and not something a caller can be asked
    /// to arrange.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);

    /// <summary>
    /// How many operation-key lookups were attempted, refused ones included.
    /// </summary>
    public int LookupCalls { get; private set; }

    /// <summary>
    /// How many callers reached <see cref="RetainAsync"/> at all, counted on
    /// the way in. <see cref="Calls"/> only records hand-overs that finished,
    /// so it cannot tell a second caller that never arrived from one still
    /// inside the call.
    /// </summary>
    public int HandOverAttempts;

    /// <summary>
    /// Held open, this parks a hand-over inside custody with the claim taken
    /// and nothing committed - the exact window a simultaneous caller of the
    /// same operation key races into.
    /// </summary>
    public TaskCompletionSource? HoldHandOver { get; set; }

    /// <summary>Completes once a hand-over is parked on the hold.</summary>
    public TaskCompletionSource HandOverEntered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        Interlocked.Increment(ref HandOverAttempts);
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

        // Everything from here to the commit is what A's serializable path
        // serializes: the check for an existing intent and the creation of one
        // are one decision, so two same-key calls cannot both make it.
        var gate = gates.GetOrAdd(request.OperationKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        CaseArtifactCustodyResult result;
        try
        {
            if (HoldHandOver is { } hold)
            {
                HandOverEntered.TrySetResult();
                await hold.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
            }

            if (ThrowOnHandOver is { } fault)
            {
                // Custody read the bytes and then failed, with nothing
                // committed. The call is recorded first, so a test can prove
                // what was offered and what came of it.
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

            // The intent this operation key already has, if the accepting
            // transaction has committed one. A repeated key is answered from
            // it and initiates no storage of its own.
            var committed = await context.Set<DocumentOccurrenceEntity>()
                .AsNoTracking()
                .Where(item => item.CaseId == caseId
                    && item.OperationKey == request.OperationKey)
                .Select(item => new { item.DocumentId, item.VersionId })
                .SingleOrDefaultAsync(cancellationToken);
            result = committed is not null
                ? await ReadCommittedIntentAsync(
                    committed.DocumentId,
                    committed.VersionId,
                    cancellationToken)
                : await InitiateAsync(context, request, caseId, received, cancellationToken);
        }
        finally
        {
            gate.Release();
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
                result.DocumentId,
                result.VersionId,
                result.BoxFileId,
                result.BoxVersionId));
        }

        return result;
    }

    /// <summary>
    /// The accepting transaction: the one call under an operation key that
    /// creates the document, the version and the intent, and the only one that
    /// initiates storage for it.
    /// </summary>
    private async Task<CaseArtifactCustodyResult> InitiateAsync(
        PegasusDbContext context,
        CaseArtifactCustodyRequest request,
        Guid caseId,
        byte[] received,
        CancellationToken cancellationToken)
    {
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
                // The authority the bytes arrived under, which for this path
                // is the upload link itself and never a member of staff.
                CreatedBy = $"RequestLink:{request.Actor.SubjectId}",
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
            Interlocked.Increment(ref ProviderInitiations);
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
        // Counted before the rule is applied, so a test can prove the read was
        // attempted and refused rather than quietly skipped.
        StatusCalls++;

        // The same fence as the other status read and as the hand-over: staff
        // casework, or the exact active, unrevoked and unexpired link naming
        // its own Case (Stream A, PR 673 comments 5560737585 and 5561151076).
        // A sender that handed bytes over through this link may find out what
        // became of them; nobody else may.
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var linkId = RequireStatusAuthority(actor, caseId, context, timeProvider.GetUtcNow());
        if (linkId is { } requestLinkId)
        {
            var createdBy = $"RequestLink:{requestLinkId:D}";
            var ownsVersion = await context.Set<DocumentVersionEntity>()
                .AsNoTracking()
                .AnyAsync(item => item.Id == versionId
                    && item.DocumentId == documentId
                    && item.CreatedBy == createdBy, cancellationToken);
            if (!ownsVersion)
            {
                throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
            }
        }
        return await ReadCommittedIntentAsync(documentId, versionId, cancellationToken);
    }

    /// <summary>
    /// Finds the accepted intent one operation key produced, without offering
    /// content again. This is the read a sender whose response was lost can
    /// actually make: Stream A fences it on the exact persisted upload link,
    /// its own Case, and the link still being active — the same fence
    /// <see cref="RetainAsync"/> applies — rather than on staff casework.
    /// </summary>
    /// <remarks>
    /// Null means only that no committed intent was observed. It is never
    /// permission to start a new one: a winner still inside
    /// <see cref="RetainAsync"/> has committed nothing yet and will.
    /// </remarks>
    public async Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
        ActionActor actor,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        // Counted before the rule is applied, so a test can prove the lookup
        // was attempted and refused rather than quietly skipped.
        LookupCalls++;
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var linkId = RequireStatusAuthority(actor, caseId, context, timeProvider.GetUtcNow());

        // The document occurrence is written inside the transaction that
        // accepts the bytes, so a row here is exactly "custody committed to
        // holding what this key offered" and its absence is exactly "nothing
        // committed has been observed".
        var intents =
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals version.Id
            where occurrence.CaseId == caseId && occurrence.OperationKey == operationKey
            select new { occurrence.DocumentId, occurrence.VersionId, version.CreatedBy };
        if (linkId is { } requestLinkId)
        {
            var createdBy = $"RequestLink:{requestLinkId:D}";
            intents = intents.Where(item => item.CreatedBy == createdBy);
        }
        var intent = await intents.SingleOrDefaultAsync(cancellationToken);
        if (intent is null && linkId is not null)
        {
            var foreignIntentExists = await context.Set<DocumentOccurrenceEntity>()
                .AsNoTracking()
                .AnyAsync(item => item.CaseId == caseId && item.OperationKey == operationKey,
                    cancellationToken);
            if (foreignIntentExists)
            {
                throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
            }
        }
        return intent is null
            ? null
            : await ReadCommittedIntentAsync(intent.DocumentId, intent.VersionId, cancellationToken);
    }

    /// <summary>
    /// What custody durably holds for one exact version, and the adapter's own
    /// confirmation write, shared by both status reads so neither can drift
    /// into answering something the other would not.
    /// </summary>
    private async Task<CaseArtifactCustodyResult> ReadCommittedIntentAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Stream A's fence for the operation-key lookup: staff casework, or the
    /// exact persisted request link this Case's bytes arrived through, while
    /// that link is active, unrevoked and unexpired.
    /// </summary>
    private static Guid? RequireStatusAuthority(
        ActionActor actor,
        Guid caseId,
        PegasusDbContext context,
        DateTimeOffset nowUtc)
    {
        if (actor.Kind == ActorKind.Staff)
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            return null;
        }
        if (actor.Kind != ActorKind.RequestLink
            || !Guid.TryParseExact(actor.SubjectId, "D", out var linkId))
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
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
            // Holding is not open to a request link, and A's rule expresses
            // that as the authorization outcome it is. It must not be an
            // InvalidOperationException: that is a type custody can just as
            // easily raise after it has taken the bytes, and the command
            // treats such a fault as uncertain rather than as a refusal.
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
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

/// <summary>
/// Whether the next retention record should fail. Armed by a test, taken once.
/// </summary>
/// <remarks>
/// A hand-over that returned and could not be written down is the failure the
/// pre-hand-over claim exists to survive, and nothing a caller does from
/// outside can provoke it.
/// </remarks>
internal sealed class RetentionRecordingFault
{
    private int armed;

    public void Arm() => Interlocked.Exchange(ref armed, 1);

    public bool TakeIfArmed() => Interlocked.Exchange(ref armed, 0) == 1;
}

/// <summary>
/// The production retention store with that one failure injected. Everything
/// else - the claim, the reads, the forward-only record - is the real store's,
/// because those are what the proofs are about.
/// </summary>
internal sealed class FaultInjectingRetentionStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    RetentionRecordingFault fault) : IIncomingArtifactRetentionStore
{
    private readonly EfPublicUploadRetentionStore inner = new(dbContextFactory);

    public Task<RetainedIncomingArtifact?> FindAsync(
        string operationKey,
        CancellationToken cancellationToken) =>
        inner.FindAsync(operationKey, cancellationToken);

    public Task<bool> TryClaimHandOverAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken) =>
        inner.TryClaimHandOverAsync(occurrenceId, cancellationToken);

    public Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken) =>
        fault.TakeIfArmed()
            ? Task.FromException(new TimeoutException("the retention record timed out"))
            : inner.RecordAsync(artifact, cancellationToken);
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
