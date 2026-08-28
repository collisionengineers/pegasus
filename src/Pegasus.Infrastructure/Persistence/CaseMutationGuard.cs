using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one persistence-side adapter for the case edit guard. It reads the retained row, compares
/// the presented token against the retained hash in fixed time, and clears the lease; the refusal
/// decision itself belongs to <see cref="CaseEditAuthority"/> in Core.
/// </summary>
internal static class CaseMutationGuard
{
    public static void Require(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        long expectedCaseVersion,
        string editLeaseToken,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArchivedCaseGuard.RequireNotArchived(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(
                workflow.State,
                ignoreCase: false,
                out var lifecycleState))
        {
            throw new InvalidDataException(
                $"Case '{workflow.CaseId}' has an unrecognized lifecycle state.");
        }
        if (CaseLifecycleRules.IsTerminal(lifecycleState))
        {
            throw new CaseTerminalMutationException(workflow.CaseId);
        }

        RequireVersion(workflow, expectedCaseVersion);
        RequireLease(workflow, actor, editLeaseToken, nowUtc);
    }

    public static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        CaseEditAuthority.RequireVersion(workflow.CaseId, workflow.Version, expectedVersion);
    }

    public static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string editLeaseToken,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        CaseEditAuthority.RequireLease(
            workflow.CaseId,
            workflow.Version,
            actor,
            editLeaseToken,
            RetainedHolderKind(workflow),
            workflow.EditLeaseHolder,
            !string.IsNullOrWhiteSpace(workflow.EditLeaseTokenHash),
            workflow.EditLeaseExpiresAtUtc,
            MatchesRetainedHash(workflow.EditLeaseTokenHash, editLeaseToken),
            nowUtc);
    }

    public static void ClearLease(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.EditLeaseToken = null;
        workflow.EditLeaseTokenHash = null;
        workflow.EditLeaseRequestHash = null;
        workflow.EditLeaseHolder = null;
        workflow.EditLeaseHolderKind = null;
        workflow.EditLeaseOperationKey = null;
        workflow.EditLeaseExpiresAtUtc = null;
    }

    public static void Complete(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
    }

    /// <summary>
    /// The retained holder's kind for Core to match against the caller. Null is a lease retained
    /// before the kind was recorded, which Core treats as nobody's; a value that is not an
    /// <see cref="ActorKind"/> is corrupt and surfaces rather than being read as a holder.
    /// </summary>
    public static ActorKind? RetainedHolderKind(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return RetainedHolderKind(workflow.EditLeaseHolderKind);
    }

    public static ActorKind? RetainedHolderKind(string? retainedHolderKind)
    {
        if (retainedHolderKind is null)
        {
            return null;
        }

        return Enum.TryParse<ActorKind>(retainedHolderKind, ignoreCase: false, out var kind)
            ? kind
            : throw new InvalidDataException(
                $"An unrecognized edit lease holder kind '{retainedHolderKind}' is retained.");
    }

    /// <summary>
    /// A retained hash that cannot be read is a hash the presented token cannot be proven against,
    /// so it refuses like any other mismatch rather than surfacing a format failure.
    /// </summary>
    private static bool MatchesRetainedHash(string? retainedHash, string? presentedToken)
    {
        if (string.IsNullOrWhiteSpace(retainedHash) || string.IsNullOrWhiteSpace(presentedToken))
        {
            return false;
        }

        byte[] retained;
        try
        {
            retained = Convert.FromHexString(retainedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken));
        return retained.Length == presented.Length
            && CryptographicOperations.FixedTimeEquals(retained, presented);
    }
}
