using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// The operator-accepted eliminator procedure (decision 2026-08-03) in one Core owner:
/// candidates are every provider case matching ANY key; a candidate contradicted by the
/// message's incident date or another present identity key is eliminated; exactly one
/// survivor is an automatic association, none is no match, and several fail closed as
/// Ambiguous with the candidates recorded. A CreatedInError survivor is replaced by its
/// linked replacement case and never associated itself. No numeric confidence exists.
/// </summary>
public sealed class EvaluateIntakeCaseMatch(
    IEnumerable<IProviderCaseMatchPolicy> policies,
    ICaseMatchCandidateQueries candidateQueries)
{
    public const string ClaimReferenceKey = "claim-reference";
    public const string VehicleRegistrationKey = "vehicle-registration";
    public const string ClaimantNameKey = "claimant-name";

    private readonly IEnumerable<IProviderCaseMatchPolicy> policies =
        policies ?? throw new ArgumentNullException(nameof(policies));
    private readonly ICaseMatchCandidateQueries candidateQueries =
        candidateQueries ?? throw new ArgumentNullException(nameof(candidateQueries));

    public async Task<CaseMatchEvaluationResult?> ExecuteAsync(
        IntakeSourceReadResult readResult,
        MailRouteEvaluationResult? mailRouteDecision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        if (mailRouteDecision is not
            { Disposition: MailRouteDisposition.Accepted, SelectedRoute: { } route })
        {
            return null;
        }

        var policy = policies.SingleOrDefault(candidate =>
            string.Equals(
                candidate.WorkProviderCode,
                route.WorkProviderCode,
                StringComparison.Ordinal));
        if (policy is null)
        {
            return null;
        }

        var keys = policy.ExtractMatchKeys(readResult);
        ArgumentNullException.ThrowIfNull(keys);
        return await EvaluateAsync(route.WorkProviderCode, policy, keys, cancellationToken);
    }

    /// <summary>
    /// The same accepted eliminator procedure applied to a Principal's own
    /// DECLARED identity facts (API-01), rather than to facts read out of a
    /// message. Nothing is parsed from the submitted files: the four declared
    /// values are normalized by the provider's own <see
    /// cref="IProviderCaseMatchPolicy.DeriveIndexKeys"/> — the very method the
    /// write side uses to index cases — so read and write can never drift into
    /// two grammars, and the shared eliminator below is the only decision
    /// procedure. Returns null when no policy owns the Principal's code; the
    /// caller treats that exactly as a no-match.
    /// </summary>
    public async Task<CaseMatchEvaluationResult?> ExecuteDeclaredAsync(
        string workProviderCode,
        CaseMatchSourceData sourceData,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workProviderCode);
        ArgumentNullException.ThrowIfNull(sourceData);

        var policy = policies.SingleOrDefault(candidate =>
            string.Equals(
                candidate.WorkProviderCode,
                workProviderCode,
                StringComparison.Ordinal));
        if (policy is null)
        {
            return null;
        }

        var derived = policy.DeriveIndexKeys(sourceData);
        ArgumentNullException.ThrowIfNull(derived);
        var keys = new CaseMatchKeys(
            derived.DurableClaimToken,
            derived.NormalizedVrm,
            derived.NormalizedSurname,
            derived.NormalizedFirstInitial,
            derived.IncidentDate);
        return await EvaluateAsync(workProviderCode, policy, keys, cancellationToken);
    }

    private async Task<CaseMatchEvaluationResult> EvaluateAsync(
        string workProviderCode,
        IProviderCaseMatchPolicy policy,
        CaseMatchKeys keys,
        CancellationToken cancellationToken)
    {
        if (!keys.HasAnyKey)
        {
            return new(
                CaseMatchOutcome.NoKeys,
                null,
                null,
                keys,
                [],
                "No case-match key could be extracted from the message.",
                policy.PolicyKey,
                policy.PolicyVersion);
        }

        var candidates = await candidateQueries.FindByAnyKeyAsync(
            workProviderCode,
            keys,
            cancellationToken);

        var evaluations = candidates
            .Select(candidate => Evaluate(keys, candidate))
            .Where(evaluation => evaluation.HitKeys.Count > 0 || evaluation.Eliminations.Count > 0)
            .ToList();
        evaluations = await RedirectCreatedInErrorAsync(
            keys,
            candidates,
            evaluations,
            cancellationToken);

        var survivors = evaluations
            .Where(evaluation => evaluation.Eliminations.Count == 0)
            .ToArray();
        return survivors.Length switch
        {
            0 => new(
                CaseMatchOutcome.NoMatch,
                null,
                null,
                keys,
                evaluations,
                evaluations.Count == 0
                    ? "No case of the provider matches any extracted key."
                    : "Every candidate case was eliminated by contradictory identity evidence.",
                policy.PolicyKey,
                policy.PolicyVersion),
            1 => new(
                CaseMatchOutcome.UniqueMatch,
                survivors[0].CaseId,
                survivors[0].RedirectedFromCaseId,
                keys,
                evaluations,
                $"Exactly one candidate case survived on {string.Join(", ", survivors[0].HitKeys)} with no contradictory identity evidence.",
                policy.PolicyKey,
                policy.PolicyVersion),
            _ => new(
                CaseMatchOutcome.Ambiguous,
                null,
                null,
                keys,
                evaluations,
                "More than one candidate case survived; competing candidates remain visible for staff sorting.",
                policy.PolicyKey,
                policy.PolicyVersion)
        };
    }

    private static CaseMatchCandidateEvaluation Evaluate(
        CaseMatchKeys keys,
        CaseMatchCandidate candidate)
    {
        var hits = new List<string>();
        var eliminations = new List<string>();

        if (keys.DurableClaimToken is not null && candidate.DurableClaimToken is not null)
        {
            if (string.Equals(
                    keys.DurableClaimToken,
                    candidate.DurableClaimToken,
                    StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(ClaimReferenceKey);
            }
            else
            {
                eliminations.Add(
                    $"The message claim reference '{keys.DurableClaimToken}' contradicts the case's '{candidate.DurableClaimToken}'.");
            }
        }

        if (keys.NormalizedVrm is not null && candidate.NormalizedVrm is not null)
        {
            if (string.Equals(
                    keys.NormalizedVrm,
                    candidate.NormalizedVrm,
                    StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(VehicleRegistrationKey);
            }
            else
            {
                eliminations.Add(
                    $"The message vehicle registration '{keys.NormalizedVrm}' contradicts the case's '{candidate.NormalizedVrm}'.");
            }
        }

        if (keys.NormalizedSurname is not null
            && keys.NormalizedFirstInitial is not null
            && candidate.NormalizedSurname is not null
            && candidate.NormalizedFirstInitial is not null)
        {
            if (string.Equals(
                    keys.NormalizedSurname,
                    candidate.NormalizedSurname,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    keys.NormalizedFirstInitial,
                    candidate.NormalizedFirstInitial,
                    StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(ClaimantNameKey);
            }
            else
            {
                eliminations.Add(
                    $"The message claimant '{keys.NormalizedFirstInitial} {keys.NormalizedSurname}' contradicts the case's '{candidate.NormalizedFirstInitial} {candidate.NormalizedSurname}'.");
            }
        }

        if (keys.IncidentDate is not null
            && candidate.IncidentDate is not null
            && keys.IncidentDate != candidate.IncidentDate)
        {
            eliminations.Add(
                $"The message incident date {keys.IncidentDate:yyyy-MM-dd} contradicts the case's {candidate.IncidentDate:yyyy-MM-dd}.");
        }

        return new(candidate.CaseId, hits, eliminations);
    }

    /// <summary>
    /// A Created in error survivor never associates itself: its linked replacement is
    /// followed (bounded, cycle-safe) and the replacement is evaluated against the
    /// message on its OWN index keys — hits and eliminations never transfer from the
    /// error case, so a replacement whose corrected identity contradicts the message is
    /// eliminated rather than inherited. A replacement without an index identity fails
    /// closed with a recorded reason.
    /// </summary>
    private async Task<List<CaseMatchCandidateEvaluation>> RedirectCreatedInErrorAsync(
        CaseMatchKeys keys,
        IReadOnlyList<CaseMatchCandidate> candidates,
        List<CaseMatchCandidateEvaluation> evaluations,
        CancellationToken cancellationToken)
    {
        const int maximumRedirectHops = 5;
        var candidatesById = candidates.ToDictionary(
            candidate => candidate.CaseId,
            candidate => candidate);
        var redirected = new List<CaseMatchCandidateEvaluation>();
        foreach (var evaluation in evaluations)
        {
            var candidate = candidatesById[evaluation.CaseId];
            if (candidate.State != CaseLifecycleState.CreatedInError)
            {
                redirected.Add(evaluation);
                continue;
            }

            var visited = new HashSet<Guid> { candidate.CaseId };
            var target = candidate;
            var hops = 0;
            var terminalReason = (string?)null;
            while (target.State == CaseLifecycleState.CreatedInError)
            {
                if (target.ReplacementCaseId is not { } replacementId)
                {
                    terminalReason =
                        "The candidate was closed as Created in error with no linked replacement; it never reopens.";
                    break;
                }

                if (!visited.Add(replacementId) || ++hops > maximumRedirectHops)
                {
                    terminalReason =
                        "The Created in error replacement chain is cyclic or too deep; the match fails closed.";
                    break;
                }

                var next = candidatesById.GetValueOrDefault(replacementId)
                    ?? await candidateQueries.FindByCaseIdAsync(replacementId, cancellationToken);
                if (next is null)
                {
                    terminalReason =
                        "The linked replacement case has no match-index identity; the match fails closed.";
                    break;
                }

                target = next;
            }

            if (terminalReason is not null)
            {
                redirected.Add(evaluation with
                {
                    Eliminations = [.. evaluation.Eliminations, terminalReason]
                });
                continue;
            }

            var targetEvaluation = Evaluate(keys, target) with
            {
                RedirectedFromCaseId = candidate.CaseId
            };
            if (targetEvaluation.HitKeys.Count == 0 && targetEvaluation.Eliminations.Count == 0)
            {
                targetEvaluation = targetEvaluation with
                {
                    Eliminations =
                    [
                        "The linked replacement case shares no identity key with the message; the match fails closed."
                    ]
                };
            }

            redirected.Add(targetEvaluation);
        }

        return redirected
            .GroupBy(evaluation => evaluation.CaseId)
            .Select(group => group
                .OrderBy(evaluation => evaluation.RedirectedFromCaseId is null ? 0 : 1)
                .Aggregate((left, right) => left with
                {
                    HitKeys = left.HitKeys
                        .Concat(right.HitKeys)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    Eliminations = left.Eliminations
                        .Concat(right.Eliminations)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                }))
            .ToList();
    }
}
