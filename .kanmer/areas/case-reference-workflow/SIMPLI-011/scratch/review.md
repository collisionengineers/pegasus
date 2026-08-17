# Review — SIMPLI-011 / PR #395 (independent reviewer agent, 2026-08-17; author: claude-code; reviewer did not implement)

## Changes (reviewer's words)
`CaseMutationPageModel.cs` new abstract base (moved plumbing; one private `ExecuteCommandAsync` core with two forwards); `Details.cshtml.cs` 1938 → 633 lines / 10 deps keeping the workspace only; six new pages (`Workflow` 7, `Tasks` 7, `Custody` 6, `Vehicle` 3, `Closure` 4, `Eva/Download` 1) with verbatim handlers — a whitespace-normalised extraction of every handler from the merge-base file matched 33/34 byte-for-byte; the 34th (`EvaDownload` → `OnPostAsync`) differs only by name and log message. Partials: 29 forms retargeted, all with `asp-route-id`; six workspace forms ambient; EVA form fields match `OnPostAsync`. `Documents/Export` adopts the base with its three refusal paths preserved. Tests: harness + five page tests, retargeted existing tests, shared `TypeInspection`. Docs: one implementation-map row. `Pegasus.slnx`, Core, `Details.cshtml` untouched.

## Comments and dispositions
- C1 (non-blocking) coverage over-claim — `RenewLease`/`ReleaseLease` (2 of the research's 22) stayed on the workspace and had no endpoint test. **Fixed in PR** (`ec0c2220`): `CaseEditModeWebTests.cs`; report corrected.
- C2 (non-blocking) undisclosed deviation — lease-loss path checked once, not once per page. **Fixed** (listed under Deviations, with the reason).
- C3 (non-blocking, one-line) `[ResponseCache(NoStore)]` dropped from the six new pages. **Fixed in PR** (`ec0c2220`).
- C4 (non-blocking) EVA log message text changed although the level did not. **Fixed** (report says so).
- C5 (non-blocking) `Save`/`ConfirmCompleteness` staying on `DetailsModel` had no recorded reason. **Fixed** (open question 4 amended).
- C6 (non-blocking) implementation-map wording ("every action redirects") vs the file-answering pages; handler count is the partials' count. **Fixed** (`ec0c2220` doc row; report says "workspace partials: 34, folder total 38").
- C7 (bookkeeping) checklist ticks / dependency count. **Fixed** before the review landed.
- C8 (informational) CI `sql-integration (1)` failed on a GitHub `setup-dotnet` 503; shards 2/3, browser, unit green. **Re-running** via the follow-up push.

## Verdict
**PASS** — merge once CI is green. Reviewer confirmed: plan covers the ticket; implementation covers the plan (deviations disclosed); behaviour preserved; base contains only verbatim moves plus the four documented simplifications; tests genuinely behavioural and complete for the six pages; docs consistent; architecture invariants intact.
