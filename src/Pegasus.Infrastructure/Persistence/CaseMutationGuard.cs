using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
            RetainedHolderKind(workflow.EditLeaseHolderKind),
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

/// <summary>
/// The one operation-replay probe for every case mutation. A retained workflow
/// event for the same operation key is the record of that operation: an exact
/// hash match means the caller is retrying and must be served the current
/// projection, and a different hash means the same key was reused for different
/// inputs, which is a conflict rather than a second write. The comparison is
/// fixed-time so a caller cannot learn a retained hash by measuring it.
/// </summary>
internal static class CaseOperationReplay
{
    public static async Task<bool> FindAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId && item.OperationKey == operationKey,
                cancellationToken);
        if (replay is null)
        {
            return false;
        }

        if (!FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }

        return true;
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != 64 || right.Length != 64
            || left.Any(character => !char.IsAsciiHexDigit(character))
            || right.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }
}

/// <summary>
/// The one owner of the history triple every case mutation writes: the workflow
/// event that carries the operation key and request hash, the action-history
/// row that carries the before/after payload, and the case-history row the
/// operator timeline reads. Three copies of this used to drift apart field by
/// field; a mutation that wrote only two of the three left the record with a
/// version bump nothing explains.
/// </summary>
internal static class CaseMutationHistory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static void Add(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventType,
        string requestHash,
        long beforeVersion,
        long afterVersion,
        string beforeJson,
        string afterJson,
        string policyVersion,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        var rolesJson = JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role),
            JsonOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            PolicyVersion = policyVersion
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            EventType = eventType,
            Actor = actor.SubjectId,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = operationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });
    }
}
