- [ ] Merge `origin/dev` into `task/plat-025-workflow-configuration`
- [ ] Re-skin `Configuration.cshtml` onto `.admin-layout` + `_AdminNav`
      (`ViewData["AdminArea"] = "configuration"`)
- [ ] Remove the banned explanatory `<aside class="notice">` copy
- [ ] Keep the two real Review checkboxes wired through the unchanged Core
      port; no business-rule change
- [ ] Omit the unsupported "Instruction completeness" / "Due work (chase
      interval)" controls (no inert placeholders)
- [ ] `OperatorLabels.cs` touched append-only, no reordering
- [ ] Web test(s) added/updated: rendered markup, handler wiring,
      non-administrator denial — no weakened/deleted assertions
- [ ] `docs/design/test-ui/catalogue.json` structural entry checked/updated
      if needed (no snapshot capture run)
- [ ] `dotnet build` Release — real exit code recorded
- [ ] Focused test filter run — real pass/fail counts recorded
- [ ] Diff scoped to this ticket's owned files only; anything outside
      reverted and reported
- [ ] Simplification pass run over the branch diff, dispositions recorded
- [ ] Backend gap (completeness/chase-interval) disposition recorded
      (defer to new ticket) — plan.md
- [ ] Commit(s) pushed
- [ ] PR opened against `dev` (not merged)
