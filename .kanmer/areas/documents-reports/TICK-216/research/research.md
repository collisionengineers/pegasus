# Research — TICK-216: accepted wording and engineer-signature boundary

## Question

Which supplied assessment wording and engineer identity/signature tuples are actually accepted and callable, and may incomplete assets ship behind a closed gate?

## Findings

1. FRD-11 accepts supplied assessment wording only as exact matching evidence and forbids placeholder or inferred content.
2. FRD-11 names one currently complete tuple: `A Patterson | M.Inst.IAEA | andy_patterson`.
3. Ed Mawdsley and Neil O'Reilly have governed signature images, but accepted qualifications are absent. FRD-11 therefore forbids selecting either person until an accepted qualification completes the tuple.
4. `docs/open-decisions.md` records the same boundary: Andy is accepted; Ed/Neil qualifications and other absent wording remain unresolved and unavailable.
5. Current `origin/dev` has a single Core `AcceptedEngineers` entry for Andy. Infrastructure embeds only `andy_patterson.png`; the renderer resource test explicitly proves Ed/Neil assets are not embedded.
6. SIMPLI-014's merged proof records matching positive/negative Core and real-Chromium evidence and explicitly says Andy is the only complete selectable tuple.
7. The earlier TICK-216 plan/open-question text over-read the operator's “all yes” answer as completing all three tuples. Approval of exact supplied content cannot create qualifications that the evidence does not contain.

## Implications

- Only Andy Patterson may be selected for assessment-report draft generation today.
- Ed Mawdsley and Neil O'Reilly remain unavailable until their exact qualifications are supplied and accepted; their signature images alone are not sufficient.
- Missing, unknown, mismatched, substituted, or custom identity/qualification/signature input fails closed.
- Unaccepted wording or incomplete identity assets must not ship as callable dormant content behind a flag.
- Generation remains draft creation; human approval is still required before issue.
