# Post-implementation report — TICK-205

## Summary

Resolved the apparent singleton-versus-pair conflict as a Kanmer-only ownership decision: ordinary assessment data has one canonical accepted repair-specification version per role/purpose; Audit deliberately requires one immutable accepted `conservative` version and one immutable accepted `maximised` version, with monetary uplift derived once by Pegasus.Core. Implementation is subsumed by TICK-093 and TICK-098, while Audit template/wording remains deferred to TICK-207. No repository or cloud change was made.

## Changes

| Record | Change | Why |
|---|---|---|
| TICK-205 body / Outcome | Replaced unresolved migration wording with the accepted one-version-per-role model, downstream ownership, deferrals, and evidence boundary | Makes the decision explicit without creating a second implementation or normative copy |
| TICK-205 links / refs | Retained TICK-093 and added TICK-098/TICK-207 relations; retained FRD-11/ADR-0025 and linked FRD-06 | Connects the decision to the aggregate, Audit behaviour, template, and governing owners |
| TICK-205 traceability | Recorded no commits/PRs and deployment `n/a` | Accurately represents a Kanmer-only reconciliation |
| Repository files | No changes | Avoids overlapping SIMPLI-014's active assessment/fee-note integration and leaves later capability implementation to its owners |

Simplification pass: **n/a — zero repository diff / Kanmer-only reconciliation**.

## Governing docs

- **FRD-06 met:** authorised-human/Core ownership, source provenance, and correction-by-superseding-version remain intact for each role. TICK-093 owns the future shared aggregate and any normative FRD-06 implementation change.
- **FRD-11 met:** exact accepted-version binding, deterministic Core computation, immutable provenance, fail-closed selection, and correction rather than overwrite remain intact. TICK-098 owns later Audit behaviour after its prerequisites; TICK-205 does not race SIMPLI-014's current FRD-11 work.
- **ADR-0025 met:** policy remains in Core and future rendering stays within the integrated application adapter. No package, service, host, template family, runtime, or deployment unit was added.

No governing document was modified.

## Risks / follow-ups

- [[TICK-093]] must implement the shared versioned repair-specification aggregate before Audit can have two durable accepted role versions without duplication.
- [[TICK-098]] remains responsible for compatible-basis validation, exact pair selection, Core-derived monetary uplift, and immutable report binding.
- [[TICK-207]] remains the required deferred step for representative Audit layout/wording. Until approval, Audit rendering must be unavailable and assessment/rendererref1 samples cannot be repurposed.
- Percentage uplift remains unavailable until its denominator and rounding are separately accepted.
- [[SIMPLI-014]] remains assessment/fee-note only. Inspection found no Audit/conservative/maximised/uplift implementation in its active Reports/template files.
- There is intentionally no new PR: an empty or duplicate repository change would violate the approved zero-diff plan.

## Verification hand-off

On merged `dev`:

1. `rg -n -C 2 "ENG-01|RPT-03|conservative|maximised|uplift" docs/capabilities.md` should show ENG-01 as one canonical repair specification and RPT-03 as the intentional conservative/maximised pair requiring both accepted versions.
2. Read TICK-093 research to confirm it owns the shared versioned aggregate and treats Audit as the role-labelled exception.
3. Read TICK-098 research to confirm it consumes the same dual-version model and remains blocked on aggregate/template prerequisites.
4. Read TICK-205 open questions to confirm the dual immutable pair is resolved and Audit rendering plus percentage uplift remain explicitly parked.
5. Confirm SIMPLI-014 remains assessment/fee-note only and no Audit model/template was added.
6. Confirm the TICK-205 branch has an empty `origin/dev...HEAD` diff and that no FRD, Core, persistence, renderer, template, infrastructure, Azure, Worker, or `main` change occurred.
7. Write proof only at the decision/ownership tier; do not claim ENG-01 or RPT-03 implementation.
