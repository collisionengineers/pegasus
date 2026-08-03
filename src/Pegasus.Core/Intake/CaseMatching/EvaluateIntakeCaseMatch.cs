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
            route.WorkProviderCode,
            keys,
            cancellationToken);

        var evaluations = candidates
            .Select(candidate => Evaluate(keys, candidate))
            .Where(evaluation => evaluation.HitKeys.Count > 0)
            .ToList();
        evaluations = RedirectCreatedInError(candidates, evaluations);

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
            && candidate.NormalizedSurname is not null
            && string.Equals(
                keys.NormalizedSurname,
                candidate.NormalizedSurname,
                StringComparison.OrdinalIgnoreCase)
            && keys.NormalizedFirstInitial is not null
            && candidate.NormalizedFirstInitial is not null
            && string.Equals(
                keys.NormalizedFirstInitial,
                candidate.NormalizedFirstInitial,
                StringComparison.OrdinalIgnoreCase))
        {
            hits.Add(ClaimantNameKey);
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

    private static List<CaseMatchCandidateEvaluation> RedirectCreatedInError(
        IReadOnlyList<CaseMatchCandidate> candidates,
        List<CaseMatchCandidateEvaluation> evaluations)
    {
        var statesById = candidates.ToDictionary(
            candidate => candidate.CaseId,
            candidate => candidate);
        var redirected = new List<CaseMatchCandidateEvaluation>();
        foreach (var evaluation in evaluations)
        {
            var candidate = statesById[evaluation.CaseId];
            if (candidate.State != CaseLifecycleState.CreatedInError)
            {
                redirected.Add(evaluation);
                continue;
            }

            if (candidate.ReplacementCaseId is not { } replacementId)
            {
                redirected.Add(evaluation with
                {
                    Eliminations =
                    [
                        .. evaluation.Eliminations,
                        "The candidate was closed as Created in error with no linked replacement; it never reopens."
                    ]
                });
                continue;
            }

            redirected.Add(evaluation with
            {
                CaseId = replacementId,
                RedirectedFromCaseId = evaluation.CaseId
            });
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
