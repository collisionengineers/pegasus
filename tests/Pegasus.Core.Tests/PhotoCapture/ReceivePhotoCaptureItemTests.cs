using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The store is the one owner of an item's batch and the Case it declares,
/// decided under the capture's lock: the upload stages under the item's own
/// member token and declares the item's Case, never the capture as the
/// request read it, so a retry after a settle or after the capture reached a
/// Case stages what the store decided and is not refused as a different
/// submission.
/// </summary>
public sealed class ReceivePhotoCaptureItemTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    [Fact]
    public async Task AnItemStagesUnderTheBatchAndCaseTheStoreGaveIt()
    {
        var read = Capture(PhotoCaptureOrigin.Website, PhotoCaptureDestination.VehicleImages);
        var declaredCase = Guid.NewGuid();
        var stored = Paperwork(batch: 2, ordinal: 4, declaredCase);
        var staged = new List<StreamedIntakeSource>();

        await Receive(stored, staged).ExecuteAsync(read, Request(stored), CancellationToken.None);

        var source = Assert.Single(staged);
        Assert.Equal(PhotoCaptureIntakeRules.MemberToken(read.Id, 2, 4), source.SourceIdentity.ExternalReceiptToken);
        Assert.Equal(declaredCase, source.DeclaredCaseId);
        Assert.Equal(DocumentSemanticRole.Other, source.DeclaredDocumentType);
    }

    /// <summary>
    /// Paperwork on a capture with no Case declares no Case and still its
    /// kind's type, so it files as Other when the capture reaches a Case
    /// (operator, 9 November 2026; R803).
    /// </summary>
    [Fact]
    public async Task AnItemWithNoCaseDeclaresNoCaseAndItsKindsDocumentType()
    {
        var read = Capture();
        var stored = Paperwork(batch: 1, ordinal: 0, declaredCaseId: null);
        var staged = new List<StreamedIntakeSource>();

        await Receive(stored, staged).ExecuteAsync(read, Request(stored), CancellationToken.None);

        var source = Assert.Single(staged);
        Assert.Null(source.DeclaredCaseId);
        Assert.Equal(DocumentSemanticRole.Other, source.DeclaredDocumentType);
    }

    /// <summary>
    /// A file sent in parts arrives with the digest the commit checked: it is
    /// recorded and handed to intake as it is, and the content is not read
    /// again only to hash it.
    /// </summary>
    [Fact]
    public async Task ADigestTheCallerCheckedIsNotComputedAgainAndIsHandedToIntake()
    {
        var stored = Paperwork(batch: 1, ordinal: 0, declaredCaseId: null);
        var staged = new List<StreamedIntakeSource>();
        var opens = 0;
        var digest = Convert.ToHexString(SHA256.HashData(Pdf));

        await Receive(stored, staged).ExecuteAsync(
            Capture(),
            Request(stored, () => opens++) with { Sha256 = digest.ToLowerInvariant() },
            CancellationToken.None);

        Assert.Equal(0, opens);
        Assert.Equal(digest, Assert.Single(staged).ContentSha256);
    }

    /// <summary>A file sent whole is read once for its digest, which intake is given rather than reading it again.</summary>
    [Fact]
    public async Task AFileWithNoDigestIsReadOnceAndIntakeIsGivenItsDigest()
    {
        var stored = Paperwork(batch: 1, ordinal: 0, declaredCaseId: null);
        var staged = new List<StreamedIntakeSource>();
        var opens = 0;

        await Receive(stored, staged).ExecuteAsync(Capture(), Request(stored, () => opens++), CancellationToken.None);

        Assert.Equal(1, opens);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf)), Assert.Single(staged).ContentSha256);
    }

    /// <summary>A part of an original sent to a link that takes none is refused as the commit would refuse it, before its bytes are kept (R801).</summary>
    [Fact]
    public async Task APartOfAnOriginalOnALinkThatTakesNoneIsRefused()
    {
        var (receive, _) = Admission([]);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => receive.AdmitPartAsync(
            Capture(PhotoCaptureOrigin.Website, PhotoCaptureDestination.VehicleImages),
            Guid.NewGuid(),
            PhotoCaptureItemKind.Overview,
            isOriginal: true,
            "image/jpeg",
            CaptureLimits.PhotoBytes,
            firstPart: true,
            CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.UnsupportedType, refusal.Code);
    }

    /// <summary>
    /// A part of a file that would carry the link past its bytes is refused
    /// as the cap reached; a part of an item the link already holds is not,
    /// as its commit is a replay.
    /// </summary>
    [Fact]
    public async Task APartPastTheLinksBytesIsRefusedUnlessTheLinkHoldsItsItem()
    {
        var held = Item(PhotoCaptureItemKind.Video) with { ContentLength = 10_000_000 };
        var (receive, _) = Admission([held]);
        var full = Capture() with { AcceptedBytes = CaptureLimits.MaximumBytesPerCapture - 1_000 };

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => receive.AdmitPartAsync(
            full, Guid.NewGuid(), PhotoCaptureItemKind.Video, false, "video/mp4", 10_000_000, firstPart: false, CancellationToken.None));
        await receive.AdmitPartAsync(
            full, held.ClientItemId, PhotoCaptureItemKind.Video, false, "video/mp4", 10_000_000, firstPart: false, CancellationToken.None);

        Assert.Equal(PhotoCaptureRefusals.CapReached, refusal.Code);
    }

    /// <summary>
    /// The first part of a video past the link's videos is refused as the
    /// cap reached; the count is read with the first part only.
    /// </summary>
    [Fact]
    public async Task TheFirstPartOfAVideoPastTheLinksVideosIsRefused()
    {
        var videos = Enumerable.Range(0, CaptureLimits.MaximumVideosPerCapture)
            .Select(_ => Item(PhotoCaptureItemKind.Video))
            .ToList();
        var (receive, store) = Admission(videos);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => receive.AdmitPartAsync(
            Capture(), Guid.NewGuid(), PhotoCaptureItemKind.Video, false, "video/mp4", 10_000_000, firstPart: true, CancellationToken.None));
        store.Calls.Clear();
        await receive.AdmitPartAsync(
            Capture(), Guid.NewGuid(), PhotoCaptureItemKind.Video, false, "video/mp4", 10_000_000, firstPart: false, CancellationToken.None);

        Assert.Equal(PhotoCaptureRefusals.CapReached, refusal.Code);
        Assert.Empty(store.Calls);
    }

    /// <summary>
    /// A retried commit whose parts were cleared is answered as a replay
    /// when the link holds the item with that digest and length, and is not
    /// when the digest differs.
    /// </summary>
    [Fact]
    public async Task ARetriedCommitIsAReplayOnlyOfTheSameBytes()
    {
        var held = Item(PhotoCaptureItemKind.Video);
        var (receive, _) = Admission([held]);

        var replay = await receive.FindReplayAsync(
            Capture(), held.ClientItemId, false, held.ContentHash.ToUpperInvariant(), held.ContentLength, CancellationToken.None);
        var other = await receive.FindReplayAsync(
            Capture(), held.ClientItemId, false, new string('b', 64), held.ContentLength, CancellationToken.None);

        Assert.Equal(PhotoCaptureItemWrite.Replayed, replay?.Write);
        Assert.Equal(held.Id, replay?.Item.Id);
        Assert.Null(other);
    }

    private static PhotoCaptureItem Paperwork(int batch, int ordinal, Guid? declaredCaseId) =>
        Item(PhotoCaptureItemKind.Paperwork, ordinal: ordinal, batch: batch) with
        {
            MediaType = "application/pdf",
            ContentLength = Pdf.Length,
            StagedReceiptId = null,
            DeclaredCaseId = declaredCaseId
        };

    private static ReceivePhotoCaptureItemRequest Request(PhotoCaptureItem item, Action? opened = null) =>
        new(
            item.ClientItemId,
            item.Kind,
            Area: null,
            Stage: null,
            item.Position,
            TakenAtUtc: null,
            LiveCamera: false,
            IsOriginal: false,
            "claim.pdf",
            item.MediaType,
            Pdf.Length,
            _ =>
            {
                opened?.Invoke();
                return ValueTask.FromResult<Stream>(new MemoryStream(Pdf, writable: false));
            });

    /// <summary>The use case over a store holding <paramref name="items"/>, and the store's forwarder to read its calls.</summary>
    private static (ReceivePhotoCaptureItem Receive, AutomaticImageIntakeTests.Forwarder Store) Admission(
        IReadOnlyList<PhotoCaptureItem> items)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.ListItemsAsync)] = _ => Task.FromResult(items);
        captures.Overrides[nameof(IPhotoCaptureStore.FindItemAsync)] = args =>
            Task.FromResult(items.FirstOrDefault(item => item.ClientItemId == (Guid)args[1]!));
        return (
            new ReceivePhotoCaptureItem(
                (IPhotoCaptureStore)captures.Proxy,
                (IIntakeSubmission)AutomaticImageIntakeTests.Forwarder.For<IIntakeSubmission>(null).Proxy,
                originals: null!,
                new FixedClock()),
            captures);
    }

    /// <summary>The use case over a store that answers with <paramref name="stored"/> as a replay with no receipt, and an intake that records what it is asked to stage.</summary>
    private static ReceivePhotoCaptureItem Receive(PhotoCaptureItem stored, List<StreamedIntakeSource> staged)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.AddItemAsync)] =
            _ => Task.FromResult(new PhotoCaptureItemWriteResult(PhotoCaptureItemWrite.Replayed, stored));
        captures.Overrides[nameof(IPhotoCaptureStore.RecordItemReceiptAsync)] = _ => Task.CompletedTask;
        var intake = AutomaticImageIntakeTests.Forwarder.For<IIntakeSubmission>(null);
        intake.Overrides[nameof(IIntakeSubmission.ReceiveStreamedAsync)] = args =>
        {
            staged.Add((StreamedIntakeSource)args[0]!);
            return Task.FromResult(new ReceivedIntake(Guid.NewGuid(), IsDuplicate: false));
        };
        intake.Overrides[nameof(IIntakeSubmission.PublishCommittedAsync)] = _ => Task.CompletedTask;
        return new(
            (IPhotoCaptureStore)captures.Proxy,
            (IIntakeSubmission)intake.Proxy,
            originals: null!,
            new FixedClock());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
