# Post-implementation report — ENG-028

Implemented by `gpt-5.6-terra` (xhigh). Every number below was **re-run by the
orchestrator**, not taken on report.

## Outcome

[[ENG-026]]'s named estimates now have operator entry points. Five capabilities,
each with a rendered control posting to a handler that calls the Core port
ENG-026 shipped — the table is in `plan/plan.md`.

The Send-to-Claude dialog is wired with a real caller at `Index.cshtml.cs:602`
but is **not claimed as delivered**: `docs/operations.md` records
`Features:SendToAi` as OFF in production, and ADR-0031 blocks activation pending
a non-preview transport decision. D21's closed-gate row applies. The lane
declared this itself rather than counting it, which is the correct call.

## Files

`Index.cshtml` (+368), `Index.cshtml.cs` (+810), `OperatorLabels.cs` (+2),
`AssessmentEstimateImportWebTests.cs` (+305). 1160 insertions, 325 deletions.
**No `Core/` change, no migration, no package, no new project.** No file owned by
an in-flight lane was touched.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | exit 0, 0 warnings, 0 errors, **0 `CS####`** |
| `dotnet test … --filter "FullyQualifiedName~AssessmentEstimateImportWebTests"` | **9 passed, 0 failed, 0 skipped** |

## Assertion integrity — the part that needed real scrutiny

The raw diff shows **35 removed `Assert.` lines**. That is precisely the shape a
weakened test suite takes, so it was examined line by line rather than accepted:

- **Total assertions in the changed file rose 74 → 90.**
- **Every retained test kept or gained** assertions — 28→29, 5→5, 5→5, 6→6, 4→4.
- Two whole tests were removed; four were added.
- The import refusals that still apply under the named-estimate model **all
  survive** with their `Assert.Empty` intact: edit-mode never entered
  (`:91`), rejected parse (`:149`), non-Engineer (`:175`).

### The two removals

`AcceptanceRecordsTheTypedCalculationBasis` is reworked as
`UseEstimateRecordsTheEngineersAcceptance` — a rename with the behaviour carried
over.

`AnExistingDraftRefusesASecondImport` is **deleted outright**. It asserted that a
second import is refused while a draft awaits acceptance — the "one draft at a
time" rule. The named-estimate model exists to replace exactly that, so its
removal is a deliberate behaviour change rather than a weakened assertion.

**This is the ticket's least-safe assumption and it is flagged as such.** If
ENG-026 did not intend several estimates to await acceptance simultaneously, the
deletion removes a fail-closed rule and must be restored in a form that fits the
new model. A reviewer should test that against ENG-026's contract, not inherit
the judgement. Recorded in `research/research.md` and on the checklist.

## The D7 seams — verified untouched

`Index.cshtml:220,223` still carry Glass's and Audatex as real
`<button type="button" class="btn" disabled aria-disabled="true">` inside
`<span class="gated" data-condition="@Model.EstimatingServiceCondition">`.
Confirmed by direct read. They belong to [[ENG-030]] under operator decision
**D23**: draw the button, never claim the capability.

## Convention alignment

Import, delete and Send to Claude each keep a query-string static target with
`data-dialog-open` as the enhancement — the same shape [[PLAT-027]] adopted in
this session. [[TICK-223]] records the rule; the two implementations now agree
rather than diverging before it lands.

## Salvage

`6b4d11db` on `task/eng-028-estimate-editor` held ~1,676 lines that would not
cherry-pick (content conflicts on all three files; no file deleted, unlike
[[CASE-012]]'s parallel branch). Reused: editor structure, handler shape, typed
line and import handling, JSON labels, test fakes. Rewritten: the integration
into current `dev`'s Assessment shell, preserving its newer evidence rail and
AI-job dialog. **The work was recovered rather than discarded or re-derived.**

## Simplification pass — 2026-08-29

- **Reuse** — ENG-026's ports, Core `EstimateTotals` and `EstimateOperations`,
  the registered JSON parser, existing CSS and the existing dialog convention.
- **Simplification** — one named-estimate path replaces the obsolete
  single-estimate acceptance UI rather than the two coexisting.
- **Efficiency** — one estimate list per render; no money policy in the browser.
- **Altitude** — handlers validate and route only; Core keeps estimate policy and
  AI-job ownership.

No unapplied findings.

## Outstanding

Independent **Claude-family** review (this was Codex-built), CI, and merge. Then
ENG-026's re-audit against merged `dev` (D15). ENG-025 also depends on this, but
additionally waits on [[ENG-029]] and [[ENG-030]].

## Commits

- `7242dfba` — feat(assessment): wire named estimate editor
- `e29ee083` — test(assessment): prove named estimate callers

PR **#630** open against `dev`, not merged.
