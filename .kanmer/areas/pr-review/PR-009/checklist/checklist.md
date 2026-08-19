# Checklist — PR-009

- [x] Create and take the isolated PR-009 branch/worktree from current `origin/dev`.
- [x] Add the unchanged-intent 80×3-list/8-photo real-Chromium regression with strong terminal-content, image, Statement/signature, every-page furniture and placeholder assertions.
- [x] Record the failing baseline in PR-009 before production changes.
- [x] Implement the smallest correction: disable Scriban's 1 MiB output truncation on the existing template context while leaving governed layout/content unchanged.
- [x] Pass the new regression and the full focused real-Chromium renderer suite.
- [x] Pass Release build and proportional Core/architecture checks with zero warnings/errors.
- [x] Run simplification lenses and record every finding/disposition in the plan.
- [x] Confirm no density selector, content cap/truncation, global auto-fit, multipass or second renderer was added.
- [x] Write PIR, commit/push, open the `dev` PR, record traceability and move Review.

## Progress notes

- 2026-08-19 failing baseline: Release Integration build passed; the new real-Chromium test failed because Statement of Truth was absent after the 80th item in all three lists and eight accepted photos.
- 2026-08-19 diagnosis: exploratory grid/table/flex/block layout changes all failed identically and were reverted. Captured HTML stopped at Scriban's exact 1,048,576-character default during the third image and omitted the tail.
- 2026-08-19 correction: `TemplateContext { LimitToString = 0 }` uses Scriban's documented unlimited mode. Regression passed 1/1; complete renderer Browser suite passed 6/6; locked restore and Release build passed with zero warnings/errors; Core reports 11/11 and dependency-direction 39/39 passed.
- 2026-08-19 simplification: final production diff is one setting on the existing renderer; diagnostic artifact writes and every exploratory layout change were removed.

- 2026-08-19: committed `f08961eaf5422474f57415355428bc189ccc16a9`, pushed the ticket branch and opened PR #419 targeting `dev`; ready for independent review.
