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
        var sweep = new PrepareDocumentThumbnails(
            new Candidates([made, failing, unrenderable, notAnImage]),
            thumbnails);

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
    }

    private sealed class RecordingThumbnails(Guid? failing = null, Guid? unrenderable = null)
        : IReadCaseDocumentThumbnail
    {
        public List<CaseDocumentThumbnailRequest> Requests { get; } = [];

        public Task<CaseDocumentThumbnail?> OpenAsync(
            CaseDocumentThumbnailRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
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
