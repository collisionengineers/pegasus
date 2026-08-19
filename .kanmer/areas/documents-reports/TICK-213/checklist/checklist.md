# Checklist — TICK-213

- [x] Confirmed SIMPLI-014 owns normal/default assessment and fee-note styling, clean page flow, no Core/caller density option, and no speculative global auto-fit.
- [x] Inspected merged Core/Infrastructure/template source: neither Core nor application callers expose density/fit; active templates use plain `<body>`; the adapter has one fixed `PdfAsync` call per artifact and no global auto-fit loop.
- [ ] Add and pass one verification-only real-Chromium stress test with long uniquely labelled repair lists and multiple accepted photos; assert multi-page continuation, terminal content, page furniture, embedded images, and no placeholders.
- [ ] Run the focused renderer Browser suite and proportional build/simplification pass; record exact evidence.
- [ ] Record the post-implementation report/outcome with the SIMPLI-014 owning merge and this test-only PR; move Review.

## Progress notes

- 2026-08-19: Existing all-four-outcome Chromium suite passed 5/5, but its representative snapshot has one item per work list and one photo. Re-planned proportionally to add the missing stress proof; no production change is authorized.


## Blocking evidence — 2026-08-19

- [x] Added the planned verification-only stress reproduction locally; test project build passed with 0 warnings/errors.
- [ ] Stress verification passed — **blocked by [[PR-009]]**. Real Chromium produced a multi-page PDF containing `Stress new part 080`, `Stress repair 080`, and `Stress operation 080`, but `Statement of Truth` was absent from extracted PDF text.
- [x] Stopped without modifying production renderer/template/CSS behavior and filed [[PR-009]] as a structured blocker.
- [ ] Focused suite, simplification, commit/PR/PIR and Review move remain pending until [[PR-009]] resolves the production defect.
