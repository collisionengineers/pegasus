using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The edit authority one command claims, runs under and gives back (FRD-14): a Triage Case's
/// Triage edit scope, held for that one save, and any other Case's edit lease. A Triage Case has
/// no Case edit lease, so its scope is its edit authority wherever a Case needs one. This is the
/// one owner of that choice; Web runs a command under it and releases it afterwards.
/// </summary>
public sealed class OneCommandEditAuthority(CaseEditLeases caseLeases, IEditScopeLeases editScopes)
{
    /// <summary>The longest operation key Core accepts on a lease or scope request.</summary>
    public const int MaximumOperationKeyLength = 100;

    private readonly CaseEditLeases _caseLeases =
        caseLeases ?? throw new ArgumentNullException(nameof(caseLeases));

    private readonly IEditScopeLeases _editScopes =
        editScopes ?? throw new ArgumentNullException(nameof(editScopes));

    /// <summary>
    /// Claims the Case's authority for one command: its Triage edit scope when
    /// <paramref name="isTriageCase"/>, otherwise its edit lease. A refused claim throws the
    /// refusal of the authority it asked for, and nothing is written.
    /// </summary>
    public async Task<OneCommandEditClaim> ClaimAsync(
        Guid caseId,
        bool isTriageCase,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (isTriageCase)
        {
            var scope = await _editScopes.ClaimAsync(
                new ClaimEditScopeRequest(EditScopeKind.Triage, caseId, expectedVersion, actor, operationKey),
                cancellationToken);
            return new(caseId, true, scope.RecordVersion, scope.Token, operationKey);
        }

        var lease = await _caseLeases.ClaimAsync(
            new ClaimCaseEditLeaseRequest(caseId, expectedVersion, actor, operationKey),
            cancellationToken);
        return new(caseId, false, lease.Version, lease.Token, operationKey);
    }

    /// <summary>Gives a claimed authority back through the port that issued it.</summary>
    public Task ReleaseAsync(
        OneCommandEditClaim claim,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return claim.IsTriageCase
            ? _editScopes.ReleaseAsync(
                new ReleaseEditScopeRequest(EditScopeKind.Triage, claim.CaseId, actor, operationKey, claim.Token),
                cancellationToken)
            : _caseLeases.ReleaseAsync(
                new ReleaseCaseEditLeaseRequest(claim.CaseId, actor, operationKey, claim.Token),
                cancellationToken);
    }

    /// <summary>
    /// Whether a refused release after a command that completed is only the write having ended
    /// the authority itself: the writer's write ends its lease or scope
    /// (<see cref="CaseEditAuthority.WriteEndsLease"/>), so the port then answers that this holder
    /// and token hold nothing (expired), or that the record has moved to another claim since
    /// (conflict). That is the expected outcome of a successful write, not a release that was due
    /// and did not happen.
    /// </summary>
    public static bool WriteEndedIt(ActionActor actor, Exception releaseRefusal)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(releaseRefusal);
        return CaseEditAuthority.WriteEndsLease(actor.Kind, keepsLease: false)
            && releaseRefusal is CaseEditLeaseExpiredException
                or CaseEditLeaseConflictException
                or EditScopeExpiredException
                or EditScopeConflictException;
    }

    /// <summary>
    /// The scope is claimed at the record's current version (R516), but a Triage save has no
    /// Edit step that draws the record again at it: a record that moved past the posted version
    /// makes the save a stale post, refused as changed before the command runs.
    /// </summary>
    public static void RequireTriageSaveAtPostedVersion(OneCommandEditClaim claim, long postedVersion)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.IsTriageCase && claim.Version != postedVersion)
        {
            throw new EditScopeVersionConflictException(
                EditScopeKind.Triage, claim.CaseId, postedVersion, claim.Version);
        }
    }

    /// <summary>
    /// An operation key derived from another, so a replay of the command replays its claim and
    /// release, within <see cref="MaximumOperationKeyLength"/>.
    /// </summary>
    public static string DeriveOperationKey(string operationKey, string suffix)
    {
        ArgumentNullException.ThrowIfNull(operationKey);
        ArgumentNullException.ThrowIfNull(suffix);
        var room = MaximumOperationKeyLength - suffix.Length;
        return (operationKey.Length > room ? operationKey[..room] : operationKey) + suffix;
    }
}

/// <summary>
/// The authority one command claimed: the Case, whether it is a Triage Case's scope, the version
/// the claim was made at, the token the command presents, and the claim's operation key.
/// </summary>
public sealed record OneCommandEditClaim(
    Guid CaseId,
    bool IsTriageCase,
    long Version,
    string Token,
    string OperationKey);
