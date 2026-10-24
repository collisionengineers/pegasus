using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// One confirmed, current image version whose gallery thumbnail has not been
/// made: the identity a thumbnail read needs. The plain gallery rendering
/// carries no rotation and no crop; a photograph's saved rotation and crop is
/// a candidate of its own, named by the same two values a tile asks with.
/// </summary>
public sealed record DocumentThumbnailCandidate(
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    string Sha256,
    long ContentLength,
    string MediaType,
    CaseAssetRotation Rotation = CaseAssetRotation.None,
    CaseAssetCrop? Crop = null);

/// <summary>
/// The versions the background sweep should make thumbnails for.
/// </summary>
public interface IListDocumentThumbnailCandidates
{
    /// <summary>
    /// Confirmed, current, thumbnailable versions with no live plain
    /// thumbnail, then the saved rotation and crop of a photograph that has
    /// no live thumbnail of that variant, at most
    /// <paramref name="maximumItems"/> in all.
    /// </summary>
    Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// A version whose thumbnail could not be made this time. It is not
    /// listed again for a while, so it cannot take a place in every run ahead
    /// of newer photographs.
    /// </summary>
    void Defer(Guid versionId);
}

public sealed record PrepareDocumentThumbnailsResult(
    int Candidates,
    int Prepared,
    int Unrenderable,
    int Failures,
    string? FirstFailure);

/// <summary>
/// Makes the plain gallery thumbnails of newly filed photographs, and the
/// thumbnails of their saved rotation and crop, before anyone opens the
/// gallery, so the first view reads a small cached rendering
/// instead of waiting for Box and the renderer.
/// </summary>
/// <remarks>
/// It runs as the system worker, one version at a time, through the same
/// <see cref="IReadCaseDocumentThumbnail"/> a gallery tile uses: the same
/// authorisation, custody-hash verification and cache. Nothing a user sees
/// changes; a version that could not be done here is made on its first view,
/// exactly as before.
/// </remarks>
public sealed class PrepareDocumentThumbnails(
    IListDocumentThumbnailCandidates candidates,
    IReadCaseDocumentThumbnail thumbnails)
{
    public const string ActorId = "document-thumbnails";

    private readonly IListDocumentThumbnailCandidates candidates =
        candidates ?? throw new ArgumentNullException(nameof(candidates));
    private readonly IReadCaseDocumentThumbnail thumbnails =
        thumbnails ?? throw new ArgumentNullException(nameof(thumbnails));

    public async Task<PrepareDocumentThumbnailsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var actor = ActionActor.SystemWorker(ActorId);
        var listed = await candidates.ListAsync(maximumItems, cancellationToken);
        var prepared = 0;
        var unrenderable = 0;
        var failures = 0;
        string? firstFailure = null;
        HashSet<Guid> deferredInRun = [];
        foreach (var candidate in listed)
        {
            // A version already deferred in this run is not opened again for
            // its other variant: the source and the decode would be the same.
            if (deferredInRun.Contains(candidate.VersionId))
            {
                continue;
            }
            if (!CaseDocumentThumbnails.IsThumbnailable(candidate.MediaType))
            {
                unrenderable++;
                Defer(candidate.VersionId);
                continue;
            }
            try
            {
                await using var thumbnail = await thumbnails.OpenAsync(
                    new CaseDocumentThumbnailRequest(
                        actor,
                        candidate.CaseId,
                        candidate.DocumentId,
                        candidate.VersionId,
                        candidate.Sha256,
                        candidate.ContentLength,
                        candidate.MediaType,
                        candidate.Rotation,
                        candidate.Crop),
                    cancellationToken);
                if (thumbnail is null)
                {
                    unrenderable++;
                    Defer(candidate.VersionId);
                }
                else
                {
                    prepared++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception)
                || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // One version's failure is that version's, a timed-out read
                // included: its first view makes the thumbnail instead, and
                // the sweep carries on.
                failures++;
                firstFailure ??= exception.GetType().Name;
                Defer(candidate.VersionId);
            }
        }
        return new(listed.Count, prepared, unrenderable, failures, firstFailure);

        void Defer(Guid versionId)
        {
            deferredInRun.Add(versionId);
            candidates.Defer(versionId);
        }
    }
}
