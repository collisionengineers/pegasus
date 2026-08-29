# Checklist — ENG-028

- [x] Estimate tabs and named-estimate editor rendered
- [x] Typed line rows, per-estimate VAT and totals, from Core `EstimateTotals`
- [x] New / Save → `ISaveEstimate` (`Index.cshtml.cs:666`)
- [x] Import estimate → `ISaveEstimate` (`Index.cshtml.cs:1092`)
- [x] Duplicate → `IDuplicateEstimate` (`Index.cshtml.cs:756`)
- [x] Use estimate / Current chip → `ISetCurrentEstimate` (`Index.cshtml.cs:841`)
- [x] Delete → `IDiscardEstimate` (`Index.cshtml.cs:794`)
- [x] Import dialog carries a static target that works without JavaScript,
      matching PLAT-027's shape ([[TICK-223]])
- [x] Glass's and Audatex left drawn, disabled, with a non-empty
      `data-condition` (D7/D23) — verified at `Index.cshtml:220,223`
- [x] `OperatorLabels.cs` appended in this lane's nested class only
- [x] No `Core/` change; no migration; no package; no new project
- [x] No file owned by an in-flight lane touched
- [x] Build — 0 errors, 0 warnings, 0 `CS####` (re-run by the orchestrator)
- [x] `AssessmentEstimateImportWebTests` — 9 passed, 0 failed
- [x] Assertion integrity — total 74 → 90; every retained test kept or gained;
      the three surviving import refusals intact (verified by the orchestrator)
- [x] Simplification pass recorded with dispositions

## Deliberately NOT claimed

- [ ] **Send to Claude is wired but not delivered.** `Features:SendToAi` is OFF
      in production (`docs/operations.md`; ADR-0031 blocks activation pending a
      non-preview transport decision). Under D21 a capability behind a closed
      gate is not delivered. Opening it is a D26 release decision.

## Outstanding

- [ ] Independent cross-model review — **required**, and must be Claude-family:
      this was built by Codex.
- [ ] CI green on the PR
- [ ] Merged to `dev`

## Then

- [ ] [[ENG-026]] re-audited against merged `dev` and returned to Done — this
      ticket is the means; unblocking ENG-026 is the end.
- [ ] [[ENG-025]] re-audited: this supplies its editor and dialog, but ENG-025
      also waits on [[ENG-029]] and [[ENG-030]].

## The one judgement a reviewer should test, not inherit

`AnExistingDraftRefusesASecondImport` was removed. It encoded "one draft at a
time", which the named-estimate model exists to replace — but if ENG-026 did not
intend several estimates to await acceptance simultaneously, that removal is a
real loss of a fail-closed rule. See `research/research.md`.
