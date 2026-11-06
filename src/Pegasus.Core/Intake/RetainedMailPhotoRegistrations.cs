using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Intake;

/// <summary>Whether a message's recorded plate reads agree on one registration.</summary>
public enum RetainedMailPhotoReadState
{
    /// <summary>The reads agree on one registration at the accepted automatic bar.</summary>
    Read,

    /// <summary>Nothing was read at the bar, or the photographs were not read.</summary>
    None,

    /// <summary>The reads accept more than one registration.</summary>
    Disagree
}

/// <summary>
/// The plate reads the Worker recorded for a retained message's photographs,
/// and the one registration they agree on when <see cref="State"/> is
/// <see cref="RetainedMailPhotoReadState.Read"/>.
/// </summary>
public sealed record RetainedMailPhotoRegistrations(
    RetainedMailPhotoReadState State,
    string? Registration,
    IReadOnlyList<string> ReadRegistrations);

/// <summary>
/// Reads what the Worker recorded when it read a retained message's
/// photographs at intake (R474, R733, R734), for a caseworker or the
/// Automation actor: the classifier agent opens a Triage only with the agreed
/// read (R732). No recognition runs, no image is read and nothing is written.
/// </summary>
public sealed class GetRetainedMailPhotoRegistrations(
    IRetainedMailQueries queries,
    IVrmSuggestionStore suggestions)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IVrmSuggestionStore suggestions =
        suggestions ?? throw new ArgumentNullException(nameof(suggestions));

    /// <returns>Null when the message is not retained or became no receipt.</returns>
    public async Task<RetainedMailPhotoRegistrations?> ExecuteAsync(
        ActionActor actor,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A retained message identifier is required.",
                nameof(messageId));
        }

        if (await queries.FindReceiptIdAsync(messageId, cancellationToken) is not { } receiptId)
        {
            return null;
        }

        var read = VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(
            await suggestions.ListForReceiptAsync(receiptId, cancellationToken));
        return read.Length switch
        {
            0 => new(RetainedMailPhotoReadState.None, null, read),
            VrmRecognitionProvisionalBar.RequiredDistinctRegistrations =>
                new(RetainedMailPhotoReadState.Read, read[0], read),
            _ => new(RetainedMailPhotoReadState.Disagree, null, read)
        };
    }
}
