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
