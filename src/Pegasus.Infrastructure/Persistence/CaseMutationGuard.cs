using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one persistence-side answer to "is this Case writable": the edit lease token, the Case
/// version, and whether the Case is open or archived. It reads the retained row, compares the
/// presented token against the retained hash in fixed time, and clears the lease; the lease and
/// version refusal decisions themselves belong to <see cref="CaseEditAuthority"/> in Core.
/// <para>
/// Loaded with <see cref="LoadAsync"/>, it answers for the Case-level paths every Case shares —
/// custody completion, artifact retention, custody retry and upload association. A Case with a
/// workflow answers through that workflow (its version, archive and edit lease); a Triage Case
/// has no workflow, so it answers through its Triage subtype (the Triage version and the Triage
/// edit scope), the same authority every Triage mutation uses.
/// </para>
/// </summary>
internal sealed class CaseMutationGuard
{
    private CaseMutationGuard(CaseEntity caseEntity, CaseWorkflowEntity? workflow, TriageEntity? triage)
    {
        Case = caseEntity;
        Workflow = workflow;
        Triage = triage;
    }

    public CaseEntity Case { get; }

    public CaseWorkflowEntity? Workflow { get; }

    public TriageEntity? Triage { get; }

    /// <summary>The version a staff write is checked against: the workflow's, or the Triage's.</summary>
    public long Version => Workflow?.Version ?? Triage!.Version;

    /// <summary>
    /// Loads the Case, tracked, with its workflow or its Triage, and the Case's
    /// Principal. Null when no Case with either exists.
    /// </summary>
    public static async Task<CaseMutationGuard?> LoadAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case).ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is not null)
        {
            return new(workflow.Case, workflow, null);
        }

        var triage = await context.Triage
            .Include(item => item.Case).ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return triage is null ? null : new(triage.Case, null, triage);
    }

    /// <summary>
    /// The version a staff write to this Case is checked against — its
    /// workflow's, or its Triage's — read without tracking. Null when no Case
    /// with either exists.
    /// </summary>
    public static async Task<long?> ReadVersionAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => (long?)item.Version)
                .SingleOrDefaultAsync(cancellationToken)
            ?? await context.Triage.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => (long?)item.Version)
                .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The archive and open-state guard of the loaded Case's workflow. With
    /// <paramref name="anyLifecycleState"/> a Case in a terminal state is
    /// accepted too (the image merge that completes a staff link); an archived
    /// Case is always refused. A Triage Case has no archive and keeps its own
    /// lifecycle, so nothing refuses it here.
    /// </summary>
    public void RequireMutable(bool anyLifecycleState = false)
    {
        if (Workflow is null)
        {
            return;
        }

        if (anyLifecycleState)
        {
            RequireNotArchived(Workflow);
        }
        else
        {
            RequireMutable(Workflow);
        }
    }

    /// <summary>
    /// Whether system work (an image merge, an automatic link) yields to a
    /// member of staff editing the Case: a held workflow edit lease. A system
    /// completion advances the version under that lease without ending it. A system completion leaves a
    /// Triage Case's version alone, so it never disturbs a Triage edit scope
    /// and nothing yields to one.
    /// </summary>
    public bool SystemWorkYields(DateTimeOffset nowUtc) =>
        Workflow is not null && CaseEditAuthority.IsHeld(Workflow.EditLeaseExpiresAtUtc, nowUtc);

    /// <summary>
    /// Whether the Case may combine with a pre-instruction record
    /// automatically, under <see cref="CaseAssociationEligibility"/>: a Case
    /// through its workflow, a Triage Case through its Triage state and link.
    /// </summary>
    public bool IsEligibleForAutomaticAssociation() =>
        Workflow is { } workflow
            ? CaseAssociationEligibility.IsFormalCaseEligible(
                Enum.Parse<CaseLifecycleState>(workflow.State), workflow.ReportSentEvidenceId is not null)
            : CaseAssociationEligibility.IsTriageCaseEligible(
                EfTriageStore.ParseState(Triage!.State), Triage.LinkedInstructionCaseId is not null);

    /// <summary>
    /// A system completion (custody) advances the workflow version and leaves
    /// the editor's lease standing, so it never ends a member of staff's edit
    /// session. It leaves a Triage Case's version alone, so custody completing
    /// never invalidates a member of staff's Triage edit scope.
    /// </summary>
    public void CompleteSystemMutation()
    {
        if (Workflow is not null)
        {
            Advance(Workflow);
        }
    }

    /// <summary>
    /// The staff authority for a write: the Case edit lease at the expected
    /// version, or the Triage edit scope at the expected Triage version. With
    /// <paramref name="anyLifecycleState"/> a Case in a terminal state is
    /// accepted too (staff linking of material to a Case in any state); an
    /// archived Case is always refused.
    /// </summary>
    public async Task RequireStaffAuthorityAsync(
        PegasusDbContext context,
        ActionActor actor,
        long expectedVersion,
        string? leaseToken,
        DateTimeOffset nowUtc,
        bool anyLifecycleState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (Workflow is not null)
        {
            if (anyLifecycleState)
            {
                RequireNotArchived(Workflow);
                RequireLease(Workflow, actor, leaseToken ?? string.Empty, nowUtc);
                RequireVersionUnderLease(Workflow, expectedVersion);
            }
            else
            {
                Require(Workflow, actor, expectedVersion, leaseToken ?? string.Empty, nowUtc);
            }

            return;
        }

        await EfEditScopeStore.RequireAsync(
            context,
            EditScopeKind.Triage,
            Triage!.CaseId,
            Triage.Version,
            expectedVersion,
            actor,
            leaseToken,
            nowUtc,
            cancellationToken);
    }

    /// <summary>
    /// Completes a write: the workflow version advances and its lease ends, or
    /// the Triage version advances and its edit scope ends — the same
    /// completion every Triage mutation makes. The Automation actor's lease or
    /// scope stands until it is released.
    /// </summary>
    public void CompleteStaffMutation(PegasusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Workflow is not null)
        {
            Complete(Workflow);
            return;
        }

        Triage!.Version = checked(Triage.Version + 1);
        EfEditScopeStore.Complete(context, EditScopeKind.Triage, Triage.CaseId, Triage.Version);
    }

    public static void Require(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        long expectedCaseVersion,
        string editLeaseToken,
        DateTimeOffset nowUtc)
    {
        RequireOpen(workflow, actor);
        RequireLease(workflow, actor, editLeaseToken, nowUtc);
        RequireVersionUnderLease(workflow, expectedCaseVersion);
    }

    /// <summary>
    /// The refusals every write shares, with no claim on the edit lease or the
    /// Case version: the actor may do casework, and the Case is neither
    /// archived nor complete. A write that is not a Case field edit and does
    /// not move the Case (a research result filed while the Engineer edits)
    /// asks only this.
    /// </summary>
    public static void RequireOpen(CaseWorkflowEntity workflow, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        RequireMutable(workflow);
    }

    /// <summary>The Case is not archived.</summary>
    public static void RequireNotArchived(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.ArchivedAtUtc is not null)
        {
            throw new CaseArchivedException(workflow.CaseId);
        }
    }

    /// <summary>The Case is neither archived nor in a terminal lifecycle state.</summary>
    public static void RequireMutable(CaseWorkflowEntity workflow)
    {
        RequireNotArchived(workflow);
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
    }

    /// <summary>
    /// Loads the Case's workflow tracked and requires it mutable, so a caller
    /// that goes on to change it reads it once.
    /// </summary>
    public static async Task<CaseWorkflowEntity> RequireMutableAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        RequireMutable(workflow);
        return workflow;
    }

    public static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        CaseEditAuthority.RequireVersion(workflow.CaseId, workflow.Version, expectedVersion);
    }

    /// <summary>
    /// The version rule for a write whose lease the same command proves: system work that moved
    /// the version since the page read it does not refuse the holder
    /// (<see cref="CaseEditAuthority.RequireVersionUnderLease"/>).
    /// </summary>
    public static void RequireVersionUnderLease(CaseWorkflowEntity workflow, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        CaseEditAuthority.RequireVersionUnderLease(workflow.CaseId, workflow.Version, expectedVersion);
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

    public static void RequireHeartbeat(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string editLeaseToken,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        CaseEditAuthority.RequireHeartbeat(
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

    /// <summary>
    /// A write under the lease: the version advances and the lease it was made under ends or
    /// stays as <see cref="CaseEditAuthority.WriteEndsLease"/> says.
    /// </summary>
    public static void Complete(CaseWorkflowEntity workflow, bool keepsLease = false)
    {
        Advance(workflow);
        EndWriteLease(workflow, keepsLease);
    }

    /// <summary>
    /// Applies <see cref="CaseEditAuthority.WriteEndsLease"/> to the lease a write was made
    /// under, once the write is made: a lease that ends is cleared from the retained row.
    /// </summary>
    public static void EndWriteLease(CaseWorkflowEntity workflow, bool keepsLease = false)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (CaseEditAuthority.WriteEndsLease(
                RetainedHolderKind(workflow.EditLeaseHolderKind), keepsLease))
        {
            ClearLease(workflow);
        }
    }

    /// <summary>
    /// System work: the version advances and whoever is editing keeps their lease. A system write
    /// never ends a member of staff's edit session (operator, 6 October 2026).
    /// </summary>
    public static void Advance(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.Version = checked(workflow.Version + 1);
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
    internal static bool MatchesRetainedHash(string? retainedHash, string? presentedToken)
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

internal static class CaseTerminalReadinessGuard
{
    public static async Task RequireNoOpenTasksAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (await context.CaseTasks.AnyAsync(
                item => item.CaseId == caseId
                    && item.State == nameof(CaseTaskState.Open),
                cancellationToken))
        {
            throw new InvalidOperationException(CaseTaskRules.OpenTasksBlockTerminal);
        }
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
        CancellationToken cancellationToken) =>
        await FindEventTypeAsync(context, caseId, operationKey, requestHash, cancellationToken) is not null;

    /// <summary>
    /// The event type of the operation already applied under this key, null
    /// when none; a different request under the key is a conflict.
    /// </summary>
    public static async Task<string?> FindEventTypeAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.OperationKey == operationKey)
            .Select(item => new { item.EventType, item.RequestHash })
            .SingleOrDefaultAsync(cancellationToken);
        if (replay is null)
        {
            return null;
        }

        if (!FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }

        return replay.EventType;
    }

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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

    /// <summary>
    /// <paramref name="noteReason"/> is the line the Case's timeline rows carry
    /// when it is not the action's own: a save session's one note lists every
    /// field the session changed (R466), while the action-history row keeps
    /// the account of this save alone.
    /// </summary>
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
        DateTimeOffset occurredAtUtc,
        string? noteReason = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(actor);
        var timelineReason = noteReason ?? reason;
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
            Reason = timelineReason,
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
            Reason = timelineReason,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = operationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });
    }
}
