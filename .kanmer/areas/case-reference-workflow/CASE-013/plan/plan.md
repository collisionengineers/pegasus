# Plan

Committed in `ca564ac5`.

## Both questions the ticket asked, answered from production

**1. Why did intake record the instruction as incomplete?** It did not evaluate anything.
`AllocateIntake.AttemptAutomaticAsync` passed a hardcoded
`EmptyCompleteness = (false, false, false, false)` for every automatically created case.

**2. Should an image-based audit require `ImagesConfirmedByStaff`?** No — and the product
already said so. `CaseCompleteness.IsReadyForReview(automaticallyDefinitive)` waives both
staff-review flags for an automatic intake. But **that method had no callers at all**. The
acceptance policy and the custody promotion had each written their own stricter copy, and
both demanded confirmation that nobody was ever going to give.

So an automatically created case was born Not ready and could not leave, however complete
it was. That is what "details incomplete, but unclear why" was.

## The change

1. The automatic route records what it knows. It runs only for a receipt already decided
   `IntakeDecision.CaseCreated` — a definitive authorised instruction with its evidence
   retained — so instruction and images are complete and the staff flags are false,
   because staff genuinely have not confirmed anything.
2. `CaseCompletenessPolicy` applies the waiver the Core rule always described, keeping the
   configuration toggles intact.
3. `AcceptIntake` decides "automatic" from the actor: a system-worker actor is the
   pipeline's own allocation; staff acceptance is never exempt.
4. The custody promotion calls `IsReadyForReview` rather than restating it. That call is
   behaviour-preserving on its own — it is the same condition — but it removes the third
   copy and gives Core's rule a caller.

## Acceptance

- An automatically definitive intake satisfies the policy without staff confirmation. ✅
- Staff acceptance still requires it. ✅
- The waiver covers staff review only — missing evidence still blocks. ✅
- The readiness rule and the acceptance policy agree, asserted directly. ✅
- Live: the QDOS26009 shape reaches Review — Phase 6.

## Simplification pass

2026-08-22. This is a de-duplication: one rule, three implementations, two of which
disagreed with the owner. Now one owner and two callers. No findings deferred.

Not attempted: naming *which* evidence is missing when a case genuinely is incomplete. The
operator asked for it, it is a separate UI change, and it is worth doing once the flags
mean something — recorded here rather than silently dropped.
