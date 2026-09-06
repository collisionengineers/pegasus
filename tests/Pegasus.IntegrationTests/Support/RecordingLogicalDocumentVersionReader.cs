using System.Collections.Concurrent;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// The one retained logical source a <see cref="RecordingLogicalDocumentVersionReader"/>
/// will serve: the identity the caller must ask for, and the exact bytes that
/// were retained under it.
/// </summary>
internal sealed record RetainedLogicalSource(
    Guid IntakeAssetId,
    Guid? CaseId,
    Guid? IntakeReceiptId,
    string Sha256,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Bytes);

/// <summary>
/// A C-owned test double for <see cref="IReadLogicalDocumentVersion"/>, the port
/// <c>ProcessQueuedIntake</c> now requires to re-read a retained source after
/// its staged copy is deleted (Stream A's INTK-027 correction).
///
/// Standalone C composes no concrete reader: A04's adapters are A-owned and are
/// supplied by the combined host. So this double is qualified boundary proof and
/// nothing more — it is never a production fallback, and its presence in a test
/// does not mean standalone C carries A04's adapters.
///
/// Unarmed it refuses everything, loudly, so a scenario that quietly grew a
/// dependency on re-reading fails by name instead of passing on a stub that
/// pretends to do work. Armed through <see cref="Serve"/> it serves exactly one
/// identity's exact retained bytes and refuses every other request, so a caller
/// that asks for a different document, case, version, hash or length cannot be
/// mistaken for one that read the source it claims to have read.
/// </summary>
internal sealed class RecordingLogicalDocumentVersionReader : IReadLogicalDocumentVersion
{
    /// <summary>
    /// What an unarmed reader says. Stated once, because the point of the
    /// refusal is that the missing registration is named where it bites.
    /// </summary>
    internal const string RefusalMessage = "standalone C has no logical version reader";

    private readonly ConcurrentQueue<ReadLogicalDocumentVersionRequest> requests = new();
    private RetainedLogicalSource? served;

    /// <summary>
    /// A reader for a scenario that must not re-read anything. Every request it
    /// receives is a hidden dependency, and it says so.
    /// </summary>
    public static RecordingLogicalDocumentVersionReader Refusing() => new();

    /// <summary>
    /// Every request this reader was asked for, in the order it was asked,
    /// including the ones it refused.
    /// </summary>
    public IReadOnlyCollection<ReadLogicalDocumentVersionRequest> Requests => requests;

    /// <summary>
    /// Arms the reader with the single retained source it will serve. Called
    /// after retention, because only then does the retained asset have the
    /// identity, hash and length the caller will ask for.
    /// </summary>
    public void Serve(RetainedLogicalSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        served = source;
    }

    public Task<LogicalDocumentContent> OpenAsync(
        ReadLogicalDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        requests.Enqueue(request);
        if (served is not { } source)
        {
            throw new InvalidOperationException(RefusalMessage);
        }

        // The real reader authorizes before it resolves anything, and the
        // queued pass reads as the system worker. A double that skipped this
        // would let a caller pass here on an actor the real port refuses.
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ExecuteSystemWork);
        if (request.DocumentId != null
            || request.VersionId != null
            || request.IntakeAssetId != source.IntakeAssetId
            || request.CaseId != source.CaseId
            || request.IntakeReceiptId != source.IntakeReceiptId
            || request.ExpectedContentLength != source.Bytes.Length
            || !string.Equals(request.ExpectedSha256, source.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{RefusalMessage} for asset {request.IntakeAssetId}, case {request.CaseId}, "
                + $"receipt {request.IntakeReceiptId}, document {request.DocumentId}, "
                + $"version {request.VersionId}, hash {request.ExpectedSha256}, "
                + $"length {request.ExpectedContentLength}: this reader serves only the "
                + $"retained source {source.IntakeAssetId} of receipt {source.IntakeReceiptId}.");
        }

        return Task.FromResult(new LogicalDocumentContent(
            new MemoryStream(source.Bytes.ToArray(), writable: false),
            DocumentId: null,
            VersionId: null,
            source.IntakeAssetId,
            source.Sha256,
            source.Bytes.Length,
            source.FileName,
            source.MediaType));
    }
}
