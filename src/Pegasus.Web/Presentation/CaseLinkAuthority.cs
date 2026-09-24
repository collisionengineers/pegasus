using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The authority a staff "Link to case" claims for its destination and the
/// link then consumes: the Case edit lease, or — for a Triage Case, which has
/// no Case workflow — the Triage edit scope, the authority every Triage
/// mutation uses.
/// </summary>
public sealed record CaseLinkAuthorityClaim(
    Guid CaseId,
    bool IsTriageCase,
    long Version,
    string Token);

public static class CaseLinkAuthority
{
    public static async Task<CaseLinkAuthorityClaim> ClaimAsync(
        IntakeAssociationDestination destination,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        IAcquireCaseEditLease acquireCaseEditLease,
        IEditScopeLeases editScopes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(acquireCaseEditLease);
        ArgumentNullException.ThrowIfNull(editScopes);
        if (destination.IsTriageCase)
        {
            var scope = await editScopes.ClaimAsync(
                new ClaimEditScopeRequest(
                    EditScopeKind.Triage,
                    destination.CaseId,
                    expectedVersion,
                    actor,
                    operationKey),
                cancellationToken);
            return new(destination.CaseId, true, scope.RecordVersion, scope.Token);
        }

        var lease = await acquireCaseEditLease.ExecuteAsync(
            new ClaimCaseEditLeaseRequest(destination.CaseId, expectedVersion, actor, operationKey),
            cancellationToken);
        return new(destination.CaseId, false, lease.Version, lease.Token);
    }
}
