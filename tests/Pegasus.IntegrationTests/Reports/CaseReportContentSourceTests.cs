using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The frozen snapshot's pinned images are reopened one at a time when the
/// renderer prints them, and their lookups are made together, once.
/// </summary>
public sealed class CaseReportContentSourceTests
{
    private static readonly byte[] Signature = [1, 2, 3];

    /// <summary>
    /// Composing reads nothing, not even the lookups. The first image to open
    /// looks every pinned image up together, once, and each image then opens its
    /// own bytes through its own handle, in whatever order the renderer asks,
    /// verified against its frozen hash. A render that prints no image, as a fee
    /// note does, makes no lookups at all.
    /// </summary>
    [Fact]
    public async Task ComposeReadsNothingAndTheFirstOpenLooksEveryImageUpOnceAndEachOpensItsOwnBytes()
    {
        var caseId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [StaffRole.Engineer]);
        byte[][] contents =
        [
            "a bonnet photograph"u8.ToArray(),
            "an offside photograph"u8.ToArray(),
            "a rear photograph"u8.ToArray()
        ];
        var images = contents.Select((bytes, index) => Image(bytes, index + 1)).ToArray();
        var reader = new PreparedReader(images.Zip(contents).ToDictionary(
            pair => pair.First.VersionId, pair => pair.Second));
        var source = new EfCaseReportContentSource(
            reader,
            new OneSignOffEngineer(new(staffId, "Ed Mawdsley", null, Signature, "image/png", true)));
        var snapshot = SnapshotOf(caseId, staffId, images);

        var composed = await source.ComposeAsync(snapshot, actor, CancellationToken.None);

        Assert.Equal(0, reader.PrepareCalls);
        Assert.Equal(
            images.Select(image => image.VersionId),
            composed.Photos.Select(photo => photo.VersionId!.Value));

        // Opened out of order: one lookup for all of them, and each its own bytes.
        Assert.Equal(contents[2], await composed.Photos[2].OpenAsync(CancellationToken.None));
        Assert.Equal(1, reader.PrepareCalls);
        Assert.Equal(contents[0], await composed.Photos[0].OpenAsync(CancellationToken.None));
        Assert.Equal(contents[1], await composed.Photos[1].OpenAsync(CancellationToken.None));
        Assert.Equal(1, reader.PrepareCalls);
        Assert.Equal(caseId, reader.PreparedCaseId);
        Assert.Same(actor, reader.PreparedActor);
        Assert.Equal(
            images.Select(image => (image.DocumentId, image.VersionId, image.Sha256, image.ContentLength)),
            reader.Prepared.Select(read => (
                read.DocumentId, read.VersionId, read.ExpectedSha256, read.ExpectedContentLength)));
        Assert.Equal(
            [images[2].VersionId, images[0].VersionId, images[1].VersionId],
            reader.Opened);

        var noImages = new PreparedReader([]);
        var feeNote = await new EfCaseReportContentSource(
                noImages,
                new OneSignOffEngineer(new(staffId, "Ed Mawdsley", null, Signature, "image/png", true)))
            .ComposeAsync(SnapshotOf(caseId, staffId, []), actor, CancellationToken.None);
        Assert.Empty(feeNote.Photos);
        Assert.Equal(0, noImages.PrepareCalls);
    }

    /// <summary>
    /// An image whose stored bytes are not the ones the generation froze is
    /// refused when it opens, however the lookups were made. The stored bytes
    /// are the frozen length with one byte changed, so it is the hash that
    /// refuses them, not a short read.
    /// </summary>
    [Fact]
    public async Task AnImageWhoseBytesChangedIsRefusedWhenItOpens()
    {
        var caseId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var frozen = "the photograph the generation froze"u8.ToArray();
        var stored = frozen.ToArray();
        stored[^1] ^= 0x01;
        var image = Image(frozen, 1);
        var reader = new PreparedReader(new()
        {
            [image.VersionId] = stored
        });
        var source = new EfCaseReportContentSource(
            reader,
            new OneSignOffEngineer(new(staffId, "Ed Mawdsley", null, Signature, "image/png", true)));

        var composed = await source.ComposeAsync(
            SnapshotOf(caseId, staffId, [image]),
            ActionActor.Staff(staffId, [StaffRole.Engineer]),
            CancellationToken.None);

        Assert.Equal(frozen.Length, stored.Length);
        var refused = await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => composed.Photos[0].OpenAsync(CancellationToken.None));
        Assert.StartsWith("The stored version of photograph-1.png has changed.", refused.Message, StringComparison.Ordinal);
        Assert.Equal([image.VersionId], reader.Opened);
    }

    /// <summary>
    /// The lookups belong to no one image and no one compose. A first open that
    /// is cancelled while they run, and a compose whose token is cancelled with
    /// it, leave them running: an image that opens after it still gets its
    /// bytes, from the same single lookup.
    /// </summary>
    [Fact]
    public async Task ACancelledFirstOpenDoesNotStopASecondOpen()
    {
        var caseId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [StaffRole.Engineer]);
        byte[][] contents = ["a bonnet photograph"u8.ToArray(), "an offside photograph"u8.ToArray()];
        var images = contents.Select((bytes, index) => Image(bytes, index + 1)).ToArray();
        var reader = new PreparedReader(images.Zip(contents).ToDictionary(
            pair => pair.First.VersionId, pair => pair.Second))
        {
            Gate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var source = new EfCaseReportContentSource(
            reader,
            new OneSignOffEngineer(new(staffId, "Ed Mawdsley", null, Signature, "image/png", true)));
        using var compose = new CancellationTokenSource();
        var composed = await source.ComposeAsync(SnapshotOf(caseId, staffId, images), actor, compose.Token);

        using var firstOpen = new CancellationTokenSource();
        var first = composed.Photos[0].OpenAsync(firstOpen.Token);
        await reader.PrepareStarted.WaitAsync(TimeSpan.FromSeconds(30));
        await firstOpen.CancelAsync();
        await compose.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        reader.Gate.SetResult();
        Assert.Equal(contents[1], await composed.Photos[1].OpenAsync(CancellationToken.None));
        Assert.Equal(contents[0], await composed.Photos[0].OpenAsync(CancellationToken.None));
        Assert.Equal(1, reader.PrepareCalls);
    }

    private static CaseReportSnapshotImage Image(byte[] bytes, int order) => new(
        OccurrenceId: Guid.NewGuid(),
        VersionId: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        ContentLength: bytes.Length,
        Sha256: Convert.ToHexStringLower(SHA256.HashData(bytes)),
        ContentType: "image/png",
        FileName: $"photograph-{order}.png",
        Role: CaseAssetReportRole.Supporting,
        Order: order,
        Rotation: CaseAssetRotation.None,
        Crop: CaseAssetCrop.Full,
        BoxFileId: $"box-file-{order}",
        BoxVersionId: $"box-version-{order}");

    private static CaseReportGenerationSnapshot SnapshotOf(
        Guid caseId, Guid staffId, IReadOnlyList<CaseReportSnapshotImage> images)
    {
        var input = AssessmentReportDraftWebTests.ReadyInput(caseId);
        var report = AssessmentReportProjection.Project(input).Snapshot!;
        var estimate = input.CurrentEstimate!;
        return new CaseReportGenerationSnapshot(
            caseId, 0, "CE-100", "operation", CaseReportActor.None,
            new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero),
            staffId, Convert.ToHexStringLower(SHA256.HashData(Signature)), "image/png",
            estimate.SpecificationId, estimate.Version, report.Costs, report.EngineerValue,
            Guid.Empty, report.Content, report.Guides, report.ReportDate,
            report.ReportDateOverridden, report.AgreedFee, report.FeeDescriptionLines,
            [], images, report.PayloadVersion, "renderer/test", report with { Photos = [] })
        {
            CurrentEstimate = estimate
        };
    }

    /// <summary>
    /// A reader that serves only through prepared handles, and records what it
    /// was asked to prepare and which handles were opened.
    /// </summary>
    private sealed class PreparedReader(Dictionary<Guid, byte[]> bytesByVersion) : IReadLogicalDocumentVersion
    {
        private readonly object guard = new();
        private readonly List<Guid> opened = [];
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int prepareCalls;

        public int PrepareCalls => Volatile.Read(ref prepareCalls);

        /// <summary>When set, the lookups wait for it, and stop if their token is cancelled.</summary>
        public TaskCompletionSource? Gate { get; init; }

        /// <summary>Completes when the lookups have begun.</summary>
        public Task PrepareStarted => started.Task;

        public Guid PreparedCaseId { get; private set; }

        public ActionActor? PreparedActor { get; private set; }

        public IReadOnlyList<LogicalDocumentVersionRead> Prepared { get; private set; } = [];

        public IReadOnlyList<Guid> Opened
        {
            get
            {
                lock (guard)
                {
                    return [.. opened];
                }
            }
        }

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The report must read its images through the prepared handles.");

        public async Task<IReadOnlyList<PreparedLogicalDocumentRead>> PrepareAsync(
            ActionActor actor,
            Guid caseId,
            IReadOnlyList<LogicalDocumentVersionRead> versions,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref prepareCalls);
            PreparedCaseId = caseId;
            PreparedActor = actor;
            Prepared = versions;
            started.TrySetResult();
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }
            return [.. versions.Select(version => new PreparedLogicalDocumentRead(token =>
            {
                lock (guard)
                {
                    opened.Add(version.VersionId);
                }
                return Task.FromResult(new LogicalDocumentContent(
                    new MemoryStream(bytesByVersion[version.VersionId], writable: false),
                    version.DocumentId,
                    version.VersionId,
                    null,
                    version.ExpectedSha256,
                    version.ExpectedContentLength,
                    "photograph.png",
                    "image/png"));
            }))];
        }
    }

    private sealed class OneSignOffEngineer(SignOffEngineerProfile profile) : IStaffAccountQueries
    {
        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>([profile]);

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
