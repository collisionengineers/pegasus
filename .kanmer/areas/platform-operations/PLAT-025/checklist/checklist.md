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
- [x] Focused test filter run — real pass/fail counts recorded (3/0, plus a
      6/0 regression check on the pre-existing admin route test)
- [x] Diff scoped to this ticket's owned files only; anything outside
      reverted and reported (none found outside scope)
- [x] Simplification pass run over the branch diff, dispositions recorded
- [x] Backend gap (completeness/chase-interval) disposition recorded
      (deferred to PLAT-062) — plan.md
- [x] Commit(s) pushed
- [ ] PR opened against `dev` (not merged)
