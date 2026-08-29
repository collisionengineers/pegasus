## 2026-08-28 — round 2 regression fix

Merged `origin/dev` (9868cf58) clean. Fixed three port regressions in
`Pages/Triage/Details.cshtml`, all pinned by `QdosTriageIntegrationTests`
and all found one at a time (each fix revealed the next):

1. L221 `Available once a finding is recorded` — Complete was `@if
   (canComplete)`; restored to the `.gated`/`data-condition` shape.
2. L317 `Post-send correction` — panel now takes that title when
   `correction` is true.
3. L477 `Permanent history` — panel renamed back from "Notes"
   (FRD-03:43 owns the term; Triage has no note entity).

Key evidence that settled the D7 tension: `.gated` is a live design-system
convention, `site.css:1893-1911`, used for **state** gates by already-merged
lanes — `Cases/Details.cshtml:269` and `Cases/Assessment/Index.cshtml:765`.
Round 1's "replaced by the per-state convention used across the workspace
pages" was simply wrong about the codebase.

No assertion touched. Build exit 0. Focused filter 9/9 pass; lane's other
owned classes 15 passed / 6 skipped. Pushed 0578835e; PR #605 body rewritten.
Moved to `review`. Did not merge; did not write proof.

## 2026-08-29 — round 3 (adversarial verification remediation)

Commit `d39db016`, pushed; PR #605 head is that SHA, still OPEN, base `dev`.
Not merged, no proof written, stage unchanged (`review`).

Findings closed: 1 major (deferred to UIIMP-012 with a measured
counter-experiment), 4 minors (2 fixed, 1 half-fixed/half-rejected with
evidence, 1 deferred to PLAT-061). Full dispositions in the plan under
"Review findings — dispositions (round 2)".

Key measurement worth keeping: renaming the Triage history heading to
EPIC-011 §1.5's "Notes" builds green but fails
`QdosTriageIntegrationTests.cs:477` (`Failed: 1, Passed: 8, Total: 9`).
`origin/dev` already shipped "Permanent history" at `Details.cshtml:348`, so
the contract, not the code, is the stale side.

Branch diff vs `origin/dev`: 5 files, all in lane. `tests/` byte-identical to
`origin/dev`. Working tree clean.
