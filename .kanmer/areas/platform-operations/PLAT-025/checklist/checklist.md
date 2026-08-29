- [x] Merge `origin/dev` into `task/plat-025-workflow-configuration`
- [x] Re-skin `Configuration.cshtml` onto `.admin-layout` + `_AdminNav`
      (`ViewData["AdminArea"] = "configuration"`)
- [x] Remove the banned explanatory `<aside class="notice">` copy
- [x] Keep the two real Review checkboxes wired through the unchanged Core
      port; no business-rule change
- [x] Omit the unsupported "Instruction completeness" / "Due work (chase
      interval)" controls (no inert placeholders)
- [x] `OperatorLabels.cs` touched append-only, no reordering
- [x] Web test(s) added/updated: rendered markup, handler wiring,
      non-administrator denial — no weakened/deleted assertions
- [x] `docs/design/test-ui/catalogue.json` structural entry checked — no
      change needed (route/state unchanged)
- [x] `dotnet build` Release — real exit code recorded (0)
- [x] Focused test filter run — real pass/fail counts recorded
- [x] Diff scoped to this ticket's owned files only; anything outside
      reverted and reported (none found outside scope)
- [x] Simplification pass run over the branch diff, dispositions recorded
- [x] Backend gap (completeness/chase-interval) disposition recorded
      (deferred to PLAT-062) — plan.md
- [x] Commit(s) pushed
- [x] PR opened against `dev` (not merged) — #622

Round 2 (verifier remediation, 2026-08-29):

- [x] `ViewData["AdminAutomationComposed"]` passed through so the rail lists
      the same areas as every sibling admin page — pinned by a test that fails
      on the pre-fix code
- [x] Stacked heading removed: h1 is the administration area, the panel h2
      keeps the §1.12 area label
- [x] Hardcoded "2 settings" dropped from `WorkflowConfiguration.Meta`; the
      test assertion tightened to the panel-title-meta version element
- [x] Every remaining medium/low finding dispositioned in plan.md under
      "Review findings — dispositions (round 2)"
- [x] Rebuild (exit 0) and focused filters re-run with real numbers: 4/0
      `WorkflowConfigurationWebTests`, 6/0 `AdministrationSearchAccountWebTests`,
      1/0 `TestUiSnapshotTests`
- [x] Round-2 commits pushed to `task/plat-025-workflow-configuration`
      (PR #622 updated, not merged)
