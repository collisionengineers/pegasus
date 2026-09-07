using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The two reads the Case workspace and the callback page make that
/// <see cref="IGlassRepairEstimateSessionStore"/> does not answer.
/// </summary>
/// <remarks>
/// <para>
/// The shared contract addresses a session by its id, which is everything the
/// gateway needs: it either creates a session or is handed one. A screen has
/// neither — the Estimate section knows a Case and the Engineer looking at it,
/// and the provider's redirect knows only the one-use correlation it was
/// launched under. Both are answered here, in Infrastructure, because the
/// contract is frozen across streams and a second read model in Core would be
/// a second owner of the same row.
/// </para>
/// <para>
/// Neither read carries provider material: the answer is the same
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
    /// The Engineer's own newest session for a Case, or null when they have
    /// none. Scoped to the one Pegasus user on purpose: a session runs inside
    /// another Engineer's external account and is not theirs to see or resume.
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
}
