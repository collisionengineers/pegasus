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

## 2026-08-29 — Audited under the strict rule 14 (D20/D21) and KEPT in Done

An independent GPT-5.6 audit flagged this ticket, and the adjudication rejected the
flag: `CLEAR_KEEP`.

Reason: the audit measured INTK-046 against the whole of `context.md` §1.6 rather
than the ticket's own text — the over-application D20's scope clause forbids. Both
reversal grounds ("case picker" and the "Create Case from accepted instruction"
destination) appear only at `context.md:50`, the contract line, and nowhere in this
ticket's What / Owns / Verification. Owns is a file list, not a capability list.

Everything the ticket does name is wired at `b92cb9a7`:
`Pages/Unidentified/Details.cshtml:1` declares `@page "/Unidentified/{id:guid}"`;
retained-source panel at `:51`, History at `:95`, resolve dialog at `:118` posting
`asp-page-handler="Resolve"` to `Details.cshtml.cs:93 OnPostResolveAsync`, and the
destination select at `:141-148` rendering all five
`UnidentifiedResolutionTargetKind` values. Triage `/Triage/{id:guid}` dispatches
thirteen action names through `Details.cshtml.cs:97 OnPostActionAsync`.

No D21 failure either: `grep "Features:"` across `Pages/Triage/`,
`Pages/Unidentified/`, `Pages/ImageIntake/` and `Pages/Intake/` returns no match. The
single disabled control on any owned page is Triage `Complete` at
`Details.cshtml:204-206`, `disabled="@(canComplete ? null : "disabled")"` where
`canComplete = record.State == TriageState.FindingRecorded`, with its condition named
on the control — D21's legitimate "conditionally disabled with a named condition"
row, and named by hand in D21's own "What this touches" section.

Note for the record: [[CASE-025]]'s `research.md:126` named INTK-046 as the supplier
of the Triage/image queue-row projections. That was wrong — this ticket's Owns covers
only the detail pages, not any Core queue projection — and that gap is now owned by
[[CASE-032]]. It is not a finding against INTK-046.
