using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The reads the Case workspace, the callback page and the Glass's window make
/// that <see cref="IGlassRepairEstimateSessionStore"/> does not answer.
/// </summary>
/// <remarks>
/// <para>
/// The shared contract addresses a session by its id, which is everything the
/// gateway needs: it either creates a session or is handed one. A screen has
/// neither — the Estimate section knows a Case and the staff member looking at it,
/// and the provider's redirect knows only the one-use correlation it was
/// launched under; the Glass's window knows a session id but must answer only
/// its owner. These are answered here, in Infrastructure, because a second
/// read model in Core would be a second owner of the same row.
/// </para>
/// <para>
/// No read carries provider material: the answer is the same
/// <see cref="GlassRepairEstimateSession"/> the store already projects, so the
/// protected state, the cookie jar and the callback fingerprint stay where they
/// are written. A caller that means to act on a session still goes through
/// <see cref="GlassRepairEstimateGateway"/>, which re-proves the correlation
/// itself.
/// </para>
/// </remarks>
public interface IGlassRepairEstimateSessionReader
{
    /// <summary>
    /// The staff member's own newest session for a Case, or null when they have
    /// none. Scoped to the one Pegasus user on purpose: a session runs inside
    /// another staff member's external account and is not theirs to see or resume.
    /// </summary>
    Task<GlassRepairEstimateSession?> GetForCaseAsync(
        Guid caseId, Guid pegasusUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The session a one-use correlation names, found by the fingerprint the
    /// launch recorded rather than by the token itself — the token is never
    /// stored. Null when nothing was launched under it.
    /// </summary>
    Task<GlassRepairEstimateSession?> FindByCallbackAsync(
        string correlation, CancellationToken cancellationToken);

    /// <summary>
    /// The staff member's session that holds an external account now, on
    /// whichever Case it was launched from, or null when none does. The
    /// Estimate section reads it so a Case that cannot launch says where the
    /// account is rather than refusing after the fact.
    /// </summary>
    Task<GlassRepairEstimateSession?> GetLiveForUserAsync(
        Guid pegasusUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The session with this id when it belongs to the staff member, or null.
    /// Another staff member's session answers null, so the Glass's window
    /// that watches a session cannot tell it from one that does not exist.
    /// </summary>
    Task<GlassRepairEstimateSession?> GetOwnAsync(
        Guid sessionId, Guid pegasusUserId, CancellationToken cancellationToken);
}
