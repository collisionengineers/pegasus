using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// What one Case closed as Collision Engineers rejected supplies to the Case
/// rejection template (<see cref="EmailTemplatePurpose.CaseRejection"/>;
/// FRD-13 Close case, R1321 to R1327). The template, its placeholders and its
/// built-in body belong to <see cref="EmailTemplates"/>.
/// </summary>
/// <remarks>
/// A fact that was not recorded has no value, so its line is left out of the
/// rendered body. <paramref name="Reason"/> is the reason staff gave in the
/// Close case dialog; <paramref name="Greeting"/> is the report delivery's
/// (<see cref="ReportDispatchPolicy.Greeting(DateTimeOffset)"/>).
/// </remarks>
public sealed record CaseRejectionFacts(
    string? Greeting,
    string? Registration,
    string? Claimant,
    string? CaseReference,
    string? PrincipalName,
    string? Reason)
{
    /// <summary>The placeholder values this Case supplies, by placeholder name.</summary>
    public IReadOnlyDictionary<string, string?> Values() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [EmailTemplates.Greeting] = Greeting,
            [EmailTemplates.Registration] = Registration,
            [EmailTemplates.Claimant] = Claimant,
            [EmailTemplates.CaseReference] = CaseReference,
            [EmailTemplates.PrincipalName] = PrincipalName,
            [EmailTemplates.Reason] = Reason
        };

    /// <summary>
    /// A new rejection message's subject: the registration and the claimant,
    /// as a new chaser's (operator, 27 November 2026; R1327). A reply keeps
    /// "Re:" the instruction's subject.
    /// </summary>
    public string Subject() => CaseMailSubject.RegistrationAndClaimant(Registration, Claimant);
}

/// <summary>The reason a closed Case's disposition was recorded with.</summary>
public interface ICaseClosureReasonQueries
{
    /// <summary>
    /// The reason given when <paramref name="caseId"/> was last closed as
    /// <paramref name="outcome"/>; null when it never was, or no reason stands.
    /// </summary>
    Task<string?> GetAsync(Guid caseId, CaseClosureOutcome outcome, CancellationToken cancellationToken);
}

/// <summary>What a rejection e-mail opens with: a new message's subject, and the body.</summary>
public sealed record CaseRejectionEmail(string Subject, string Body);

/// <summary>
/// The Case rejection template rendered for one Case, which the e-mail
/// dialogue opens on after Close case (FRD-13 Close case; R1321). Only a Case
/// that stands closed as Collision Engineers rejected has one (R1322); for
/// any other Case there is none, so nothing is pre-filled.
/// </summary>
public sealed class RenderCaseRejectionEmail(
    RenderEmailTemplate renderTemplate,
    ICaseClosureReasonQueries closureReasons,
    TimeProvider clock)
{
    public async Task<CaseRejectionEmail?> ExecuteAsync(
        ActionActor actor,
        CaseHeader details,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(details);
        if (details.Workflow.State != CaseLifecycleState.CollisionEngineersRejected)
        {
            return null;
        }

        var facts = new CaseRejectionFacts(
            ReportDispatchPolicy.Greeting(clock.GetUtcNow()),
            details.Summary.Registration,
            details.Summary.Claimant,
            details.Summary.Reference,
            details.Summary.Principal,
            await closureReasons.GetAsync(
                details.Summary.CaseId, CaseClosureOutcome.CollisionEngineersRejected, cancellationToken)
                .ConfigureAwait(false));
        return new(
            facts.Subject(),
            await renderTemplate.ExecuteAsync(
                actor, EmailTemplatePurpose.CaseRejection, facts.Values(), cancellationToken)
                .ConfigureAwait(false));
    }
}
