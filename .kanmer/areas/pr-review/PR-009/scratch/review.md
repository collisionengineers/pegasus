# Independent review — 2026-08-19

## Changes

- `PlaywrightAssessmentReportRenderer` sets `TemplateContext.LimitToString = 0` on the existing single render path.
- `AssessmentReportRendererTests` extracts the existing composition setup into one helper and adds the real-Chromium 80×3-list/8-photo regression.

## Comments and disposition

- **Root cause — pass.** Scriban 7.2.6 source sets the default to 1,048,576 and its output writer appends an ellipsis at the limit; `LimitToString <= 0` returns the full requested output. This matches the recorded exact-boundary failing evidence. Disposition: fixed in PR by the documented existing-context setting.
- **Regression rigor — pass.** The Browser test asserts multi-page output, terminal item 080 from all three lists, every-page report reference furniture, at least eight embedded PDF images, Statement of Truth, accepted A Patterson signature identity, and absence of unresolved placeholders. The complete renderer class passed 6/6 independently through real Chromium.
- **No layout weakening — pass.** The PR changes no rendererref1 template or CSS, introduces no density selector, compact class, auto-fit, multipass, content cap, truncation, or second renderer. Normal layout and content order remain governed and unchanged.
- **Governing docs/report — pass.** The PIR lists both changed files honestly and matches the diff. FRD-11 complete-content behavior is restored without changing policy; ADR-0025's existing Infrastructure boundary is preserved.
- **Simplification — pass.** The one-line production fix reuses the existing context and the test helper removes duplicated composition setup. Exploratory layout/diagnostic changes were removed; no deferred finding is hidden.
- **CI transient — non-blocking after green retry.** Initial SQL shard 2 failed 1/171 in unrelated `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` due to a SQL deadlock victim. No PR-009 path was involved. The failed job was rerun and passed 171/171; all required checks, Browser, unit, SQL shards/coverage, documentation, source/reference checks are green.

## Verdict

PASS. No blocking or non-blocking code finding remains. Checked full ticket/plan/files/questions/PIR/checklist, PR diff and metadata, packaged Scriban implementation, local real-Chromium suite, and final required CI.
