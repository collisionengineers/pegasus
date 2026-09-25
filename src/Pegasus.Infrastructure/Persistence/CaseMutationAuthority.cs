using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The authority over a Case for the Case-level paths every Case shares —
/// custody completion, artifact retention, custody retry and upload
/// association. A Case with a workflow answers through that workflow (its
/// version, archive and edit lease); a Triage Case has no workflow, so it
/// answers through its Triage subtype (the Triage version and the Triage edit
/// scope), the same authority every Triage mutation uses.
/// </summary>
internal sealed class CaseMutationAuthority
{
    private CaseMutationAuthority(CaseEntity caseEntity, CaseWorkflowEntity? workflow, TriageEntity? triage)
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
    public static async Task<CaseMutationAuthority?> LoadAsync(
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
    /// The archive and open-state guard of a Case workflow. With
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
            ArchivedCaseGuard.RequireNotArchived(Workflow);
        }
        else
        {
            ArchivedCaseGuard.RequireMutable(Workflow);
        }
    }

    /// <summary>
    /// Whether system work (an image merge, an automatic link) yields to a
    /// member of staff editing the Case: a held workflow edit lease, whose
    /// version a system completion advances. A system completion leaves a
    /// Triage Case's version alone, so it never disturbs a Triage edit scope
    /// and nothing yields to one.
    /// </summary>
    public bool SystemWorkYields(DateTimeOffset nowUtc) =>
        Workflow is not null && CaseEditAuthority.IsHeld(Workflow.EditLeaseExpiresAtUtc, nowUtc);

    /// <summary>
    /// A system completion (custody) advances the workflow version. It leaves a
    /// Triage Case's version alone, so custody completing never invalidates a
    /// member of staff's Triage edit scope.
    /// </summary>
    public void CompleteSystemMutation()
    {
        if (Workflow is not null)
        {
            CaseMutationGuard.Complete(Workflow);
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
                ArchivedCaseGuard.RequireNotArchived(Workflow);
                CaseMutationGuard.RequireVersion(Workflow, expectedVersion);
                CaseMutationGuard.RequireLease(Workflow, actor, leaseToken ?? string.Empty, nowUtc);
            }
            else
            {
                CaseMutationGuard.Require(Workflow, actor, expectedVersion, leaseToken ?? string.Empty, nowUtc);
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
    /// Completes a staff write: the workflow version advances and its lease
    /// clears, or the Triage version advances and its edit scope ends — the
    /// same completion every Triage mutation makes.
    /// </summary>
    public void CompleteStaffMutation(PegasusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Workflow is not null)
        {
            CaseMutationGuard.Complete(Workflow);
            return;
        }

        Triage!.Version = checked(Triage.Version + 1);
        EfEditScopeStore.Complete(context, EditScopeKind.Triage, Triage.CaseId);
    }
}
