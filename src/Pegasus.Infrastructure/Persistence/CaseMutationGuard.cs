using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

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

        if (workflow.Version != expectedCaseVersion)
        {
            throw new CaseVersionConflictException(
                workflow.CaseId,
                expectedCaseVersion,
                workflow.Version);
        }

        if (string.IsNullOrWhiteSpace(editLeaseToken)
            || workflow.EditLeaseExpiresAtUtc is null
            || workflow.EditLeaseExpiresAtUtc <= nowUtc
            || string.IsNullOrWhiteSpace(workflow.EditLeaseTokenHash)
            || string.IsNullOrWhiteSpace(workflow.EditLeaseHolder))
        {
            throw new CaseEditLeaseExpiredException(workflow.CaseId);
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(editLeaseToken));
        byte[] retainedHash;
        try
        {
            retainedHash = Convert.FromHexString(workflow.EditLeaseTokenHash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "The retained case edit lease hash is invalid.",
                exception);
        }

        if (!string.Equals(workflow.EditLeaseHolder, actor.SubjectId, StringComparison.Ordinal)
            || retainedHash.Length != suppliedHash.Length
            || !CryptographicOperations.FixedTimeEquals(retainedHash, suppliedHash))
        {
            throw new CaseEditLeaseConflictException(workflow.CaseId);
        }
    }

    public static void Complete(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.Version = checked(workflow.Version + 1);
        workflow.EditLeaseToken = null;
        workflow.EditLeaseTokenHash = null;
        workflow.EditLeaseRequestHash = null;
        workflow.EditLeaseHolder = null;
        workflow.EditLeaseOperationKey = null;
        workflow.EditLeaseExpiresAtUtc = null;
    }
}
