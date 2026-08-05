using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The single owner of the decision every staff case mutation is guarded by: the case must stand at
/// the version the editor loaded, and the caller must present the live edit lease it holds. A
/// missing, expired, wrong-holder, or stale-version mutation is refused without overwriting newer
/// work, and there is no takeover, force, or bypass. Infrastructure supplies the persisted material
/// and the fixed-time token comparison; the refusal order is business policy and lives here.
/// </summary>
public static class CaseEditAuthority
{
    /// <summary>
    /// Edit lease tokens are issued as 64 hexadecimal characters and retained in a column of that
    /// exact width, so a longer presented value can never round-trip and is refused as invalid.
    /// </summary>
    public const int LeaseTokenLength = 64;

    /// <summary>
    /// True when a retained expiry is still in the future by server time. An abandoned lease
    /// expires without a sweeper, so every projection and guard asks this one question.
    /// </summary>
    public static bool IsHeld(DateTimeOffset? leaseExpiresAtUtc, DateTimeOffset nowUtc) =>
        leaseExpiresAtUtc is { } expiresAtUtc && expiresAtUtc > nowUtc;

    public static void RequireVersion(Guid caseId, long caseVersion, long expectedVersion)
    {
        if (caseVersion != expectedVersion)
        {
            throw new CaseVersionConflictException(caseId, expectedVersion, caseVersion);
        }
    }

    /// <summary>
    /// Refuses a mutation that does not present the live lease its actor holds. The caller has
    /// already compared the presented token against the retained hash in fixed time;
    /// <paramref name="presentedTokenMatchesRetainedHash"/> is false when it does not match or when
    /// the retained hash cannot be read, so an unprovable token fails closed.
    /// </summary>
    public static void RequireLease(
        Guid caseId,
        long caseVersion,
        string actorSubjectId,
        string? presentedLeaseToken,
        string? retainedLeaseHolder,
        bool hasRetainedLeaseTokenHash,
        DateTimeOffset? leaseExpiresAtUtc,
        bool presentedTokenMatchesRetainedHash,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(presentedLeaseToken)
            || !IsHeld(leaseExpiresAtUtc, nowUtc)
            || !hasRetainedLeaseTokenHash
            || string.IsNullOrWhiteSpace(retainedLeaseHolder))
        {
            throw new CaseEditLeaseExpiredException(caseId, caseVersion);
        }

        if (!string.Equals(retainedLeaseHolder, actorSubjectId, StringComparison.Ordinal)
            || !presentedTokenMatchesRetainedHash)
        {
            throw new CaseEditLeaseConflictException(caseId, caseVersion);
        }
    }
}

/// <summary>
/// How the holder of a case's edit authority is disclosed to other authorised staff. A resolved
/// account is named; an unresolvable one is described without an identifier, because the retained
/// holder is a subject identifier and an identifier is never operator-facing.
/// </summary>
public sealed record CaseEditAuthorityHolder(string? DisplayName)
{
    public static readonly CaseEditAuthorityHolder Unnamed = new(DisplayName: null);
}

public interface IDescribeCaseEditAuthorityHolder
{
    Task<CaseEditAuthorityHolder> ExecuteAsync(
        string holderSubjectId,
        ActionActor actor,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the retained holder to the staff account name other authorised staff may see, through
/// the same staff-account read the administration surface uses. Casework permission is enough:
/// the requirement gives every authorised editor sight of who holds a case, and nothing beyond the
/// account name is disclosed.
/// </summary>
public sealed class DescribeCaseEditAuthorityHolder(IStaffAccountQueries accounts)
    : IDescribeCaseEditAuthorityHolder
{
    private readonly IStaffAccountQueries _accounts =
        accounts ?? throw new ArgumentNullException(nameof(accounts));

    public async Task<CaseEditAuthorityHolder> ExecuteAsync(
        string holderSubjectId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!Guid.TryParse(holderSubjectId, out var staffId) || staffId == Guid.Empty)
        {
            return CaseEditAuthorityHolder.Unnamed;
        }

        var account = await _accounts.GetAsync(staffId, cancellationToken);
        return account is null || string.IsNullOrWhiteSpace(account.UserName)
            ? CaseEditAuthorityHolder.Unnamed
            : new(account.UserName);
    }
}
