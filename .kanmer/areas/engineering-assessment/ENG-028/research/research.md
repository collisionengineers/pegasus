# Research — ENG-028

Written alongside the work. Records what was **verified by a read-only check**
and what was assumed.

## Verified

- **[[ENG-026]]'s named-estimate ports exist and had no operator caller.**
  `ISaveEstimate`, `IDuplicateEstimate`, `ISetCurrentEstimate`,
  `IDiscardEstimate` were registered and reachable only from tests. That is the
  rule-14 gap this ticket closes.
- **The salvage commit `6b4d11db` does not cherry-pick.** Attempted onto `dev`:
  conflicts on all three source files. Crucially they are **content** conflicts
  only — no file was deleted, unlike [[CASE-012]]'s parallel branch — so the work
  was recoverable by hand rather than lost.
- **`Features:SendToAi` is OFF in production.** `docs/operations.md` records it
  as DevelopmentOffline-only, with production activation additionally requiring a
  non-preview transport decision (ADR-0031). This is why Send to Claude is wired
  but **not claimed as delivered**.
- **The D7 seams' current shape.** Glass's and Audatex at
  `Pages/Cases/Assessment/Index.cshtml:220,223` are real
  `<button ... disabled aria-disabled="true">` inside
  `<span class="gated" data-condition="...">`. Left exactly as found, per D23.
- **The static-dialog-target convention.** [[PLAT-027]]'s remediation had just
  adopted it for Disable and Review; the same shape is used here so the two do
  not diverge before [[TICK-223]] records the rule.
- **Assertion integrity**, re-run by the orchestrator: total assertions in the
  changed test file went **74 → 90**, and every retained test kept or gained
  (28→29, 5→5, 5→5, 6→6, 4→4).

## Assumed

- **That the single-draft acceptance model is genuinely superseded.** Two tests
  were removed: `AnExistingDraftRefusesASecondImport` and
  `AcceptanceRecordsTheTypedCalculationBasis`. The first encodes "one draft at a
  time", which the named-estimate model exists to replace; the second is
  reworked as `UseEstimateRecordsTheEngineersAcceptance`.

  **This is the assumption most worth challenging at review.** If ENG-026 did
  *not* intend to permit several estimates awaiting acceptance simultaneously,
  removing that refusal is a real loss. The three other import refusals —
  edit-mode never entered, rejected parse, non-Engineer — all survive with their
  `Assert.Empty` intact.

- **That duplicate and use should be offered for accepted non-current
  estimates.** The lane exposed both on the grounds that ENG-026 supports it.
  Confirm against ENG-026's contract rather than its implementation surface.

## Not established

Whether the design contract's Send-to-Claude target-percentage slider should
read the Engineer's Value from [[ENG-027]]'s `CaseValuations` rows or from the
assessment projection. ENG-027 writes both, but its operator entry point is
[[CASE-029]] and does not exist yet, so nothing populates the field today. The
dialog is wired against the projection, which is what the three existing
production readers use.
