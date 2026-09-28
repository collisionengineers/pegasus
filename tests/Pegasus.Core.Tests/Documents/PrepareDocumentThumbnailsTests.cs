using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// The background sweep makes each candidate's plain thumbnail as the system
/// worker, one at a time; one version's failure is counted and the rest still
/// run.
/// </summary>
public sealed class PrepareDocumentThumbnailsTests
{
    [Fact]
    public async Task EachCandidateIsReadAsTheSystemWorkerAndOneFailureDoesNotStopTheRest()
    {
        var made = Candidate("image/jpeg");
        var failing = Candidate("image/png");
        var unrenderable = Candidate("image/heic");
        var notAnImage = Candidate("application/pdf");
        var thumbnails = new RecordingThumbnails(failing.VersionId, unrenderable.VersionId);
        var listed = new Candidates([made, failing, unrenderable, notAnImage]);
        var sweep = new PrepareDocumentThumbnails(listed, thumbnails);

        var result = await sweep.ExecuteAsync(4);

        Assert.Equal(new PrepareDocumentThumbnailsResult(4, 1, 2, 1, nameof(IOException)), result);
        Assert.Equal(
            new[] { made.VersionId, failing.VersionId, unrenderable.VersionId },
            thumbnails.Requests.Select(request => request.VersionId));
        Assert.All(thumbnails.Requests, request =>
        {
            Assert.Equal(ActorKind.SystemWorker, request.Actor.Kind);
            Assert.False(request.IsPrepared);
        });
        // Only what could not be made waits; the made thumbnail leaves the
        // candidate set by itself.
        Assert.Equal(
            new[] { failing.VersionId, unrenderable.VersionId, notAnImage.VersionId },
            listed.Deferred);
    }

    [Fact]
    public async Task ATimedOutReadIsThatVersionsFailureNotTheSweeps()
    {
        var timedOut = Candidate("image/jpeg");
        var next = Candidate("image/jpeg");
        var thumbnails = new RecordingThumbnails(timedOut: timedOut.VersionId);
        var listed = new Candidates([timedOut, next]);

        var result = await new PrepareDocumentThumbnails(listed, thumbnails).ExecuteAsync(2);

        Assert.Equal(new PrepareDocumentThumbnailsResult(2, 1, 0, 1, nameof(TaskCanceledException)), result);
        Assert.Equal(new[] { timedOut.VersionId }, listed.Deferred);
    }

    [Fact]
    public async Task ACancelledSweepStops()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var sweep = new PrepareDocumentThumbnails(
            new Candidates([Candidate("image/jpeg")]),
            new RecordingThumbnails(honourCancellation: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sweep.ExecuteAsync(1, cancellation.Token));
    }

    [Fact]
    public async Task ASweepAsksForAtLeastOneCandidate()
    {
        var sweep = new PrepareDocumentThumbnails(new Candidates([]), new RecordingThumbnails());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sweep.ExecuteAsync(0));
    }

    private static DocumentThumbnailCandidate Candidate(string mediaType) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 10, mediaType);

    private sealed class Candidates(IReadOnlyList<DocumentThumbnailCandidate> candidates)
        : IListDocumentThumbnailCandidates
    {
        public Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
            int maximumItems,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DocumentThumbnailCandidate>>([.. candidates.Take(maximumItems)]);

        public List<Guid> Deferred { get; } = [];

        public void Defer(Guid versionId) => Deferred.Add(versionId);
    }

    private sealed class RecordingThumbnails(
        Guid? failing = null,
        Guid? unrenderable = null,
        Guid? timedOut = null,
        bool honourCancellation = false)
        : IReadCaseDocumentThumbnail
    {
        public List<CaseDocumentThumbnailRequest> Requests { get; } = [];

        public Task<CaseDocumentThumbnail?> OpenAsync(
            CaseDocumentThumbnailRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (honourCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (request.VersionId == timedOut)
            {
                // What an HttpClient timeout looks like to the caller.
                throw new TaskCanceledException("The read timed out.");
            }
            if (request.VersionId == failing)
            {
                throw new IOException("Box said later.");
            }
            return Task.FromResult(request.VersionId == unrenderable
                ? null
                : new CaseDocumentThumbnail(
                    new MemoryStream([1, 2, 3], writable: false),
                    CaseDocumentThumbnails.MediaType,
                    3,
                    request.Sha256));
        }
    }
}
