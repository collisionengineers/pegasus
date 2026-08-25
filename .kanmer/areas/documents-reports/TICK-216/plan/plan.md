# Plan — TICK-216: close the exact accepted wording/signature boundary

## Approach

Treat TICK-216 as a corrected no-code acceptance slice subsumed by [[SIMPLI-014]]. Exact supplied assessment wording is accepted only where complete. The sole complete engineer tuple is `A Patterson | M.Inst.IAEA | andy_patterson`; Ed Mawdsley and Neil O'Reilly remain unavailable pending supplied and accepted qualifications. Core validates the closed tuple, Infrastructure embeds only the accepted resource, and incomplete/mismatched/custom content fails closed. No dormant unaccepted asset is shipped.

## Governing docs

- FRD-11 already states the exact accepted/unavailable boundary; no change is required.
- `docs/open-decisions.md` already retains the missing Ed/Neil qualifications and other absent wording as open evidence; no false resolution is recorded.
- ADR-0025 keeps policy in Core and resource mapping in the Infrastructure adapter. No new boundary or ADR is needed.

## Steps

1. Correct TICK-216 research, plan, open questions, checklist, report, and Outcome so none claims that Ed/Neil have complete accepted tuples.
2. Confirm SIMPLI-014's merged Core allow-list contains only Andy and rejects incomplete, unknown, mismatched, or substituted values before adapter rendering.
3. Confirm Infrastructure embeds only Andy's byte-verified resource and tests assert that Ed/Neil resources are absent.
4. Confirm accepted assessment wording remains exact, other absent wording stays unavailable, and generation remains draft-before-human-issue.
5. Complete a zero-repository-diff report, retrospective review, and proof linked to SIMPLI-014 PR #415.

## Verification

- Read FRD-11 and `docs/open-decisions.md` for the one complete tuple and explicit Ed/Neil unavailability.
- Inspect current `origin/dev` Core/resource/test lines for one accepted entry, one embedded signature, and negative coverage.
- Use SIMPLI-014's merged 11/11 Core, 5/5 real-Chromium, 39/39 architecture, and green CI evidence.
- Do not claim Ed/Neil render evidence, invented qualifications, issue/send approval, or a TICK-216 repository diff.
