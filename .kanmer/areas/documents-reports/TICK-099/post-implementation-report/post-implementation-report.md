# Post-implementation report — TICK-099

## Summary

Recorded RPT-04 as an explicitly unsupported, unavailable, fail-closed, and deferred capability through a Kanmer-only reconciliation. The Later / 1.1.0 allocation remains a schedule entry, not activation; the imported generic `diminution-rebuttal` preset was not treated as governing evidence. No repository, renderer, cloud, or deployment change was made.

## Changes

| Record | Change | Why |
|---|---|---|
| TICK-099 body / Outcome | Added the deferral tier, unavailable state, activation prerequisites, policy owners, and prohibited substitutes | Prevents an allocated capability or generic preset from being mistaken for approved product behaviour |
| TICK-099 links / traceability | Linked [[TICK-206]] and [[SIMPLI-014]] alongside the existing upstream dependencies; recorded commits `[]`, PRs `[]`, deployment `n/a` | Makes the inactive-catalogue decision and assessment-only implementation boundary explicit |
| TICK-099 checklist / PIR | Recorded the zero-diff execution and verification hand-off | Provides a reviewable closed-boundary result without dormant implementation |
| Repository/reference files | No changes | No accepted diminution semantics, wording, layout, caller, or representative evidence exists |

Simplification pass: **n/a — zero repository diff / Kanmer-only reconciliation**.

## Governing docs

- **FRD-11 met without modification:** accepted source facts, human approval, deterministic identity, immutable artifact/hash, correction, and fail-closed behaviour remain the required future boundary. It does not define diminution percentage semantics, calculation, wording, layout, or approval evidence, so none was inferred.
- **ADR-0025 met without modification:** any future activation remains behind a Core-owned port with Infrastructure rendering inside Pegasus and a real caller. No workspace activation, API, package, MCP host, deployment unit, template descriptor, or speculative abstraction was added.
- **Capability registry preserved:** RPT-04 remains Later / 1.1.0 with “allocation only; wording and approval evidence remain required.”

No governing document was modified.

## Risks / follow-ups

- [[TICK-206]] remains authoritative for the current application allow-list: only the `rendererref1` assessment/fee-note family activates; `diminution-rebuttal` remains inactive.
- [[TICK-092]], [[TICK-093]], and [[TICK-094]] remain preparing/post-alpha/blocked and retain their upstream case and engineering policy; this ticket does not duplicate or pre-empt them.
- [[SIMPLI-014]] remains assessment/fee-note only and must expose no diminution operation.
- A future linked activation ticket must establish accepted original-case identity/version, Engineer-entered percentage meaning and precision, calculation/rounding, wording/layout, approval, correction/version linkage, caller, fail-closed behaviour, and representative evidence.
- Do not expose or adapt the workspace preset, clone assessment templates, accept free-form caller content, add placeholders/dormant descriptors/disabled-feature implementations, or infer professional/legal wording.
- There is intentionally no PR: an empty repository change would add no reviewable implementation.

## Verification hand-off

On merged `dev`:

1. `rg -n -C 2 "RPT-04|diminution" docs/capabilities.md docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` should show RPT-04 only as Later / 1.1.0 allocation with wording/approval evidence outstanding and no diminution behaviour in FRD-11.
2. `rg --files reference/rendererref1 | Sort-Object` and `rg -n -i "diminution" reference/rendererref1` should show assessment/fee-note evidence only and no diminution artifact or contract.
3. Focused `rg` in `workspaces/report-renderer` should show `diminution-rebuttal` as a generic imported catalogue preset/test, not an authorised Pegasus caller or typed RPT-04 contract.
4. Confirm [[TICK-206]] keeps that preset inactive and [[TICK-092]], [[TICK-093]], and [[TICK-094]] remain unactivated dependencies.
5. Confirm TICK-099's operator question is resolved and its activation semantics/evidence remain explicitly parked.
6. `git status --short --branch`, `git diff --stat origin/dev...HEAD`, and `git diff --name-only origin/dev...HEAD` should confirm a clean, empty repository diff.
7. Write proof only at the deferral/closed-boundary tier. Do not claim diminution rendering, a template, RPT-04 acceptance, representative parity, or deployment.
