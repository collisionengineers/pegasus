# Independent review — 2026-08-17

Reviewer: independent subagent (not the implementer)
PR: #379 at `c95e24a7093f8758fba3877359d0fabfcfd46f6b`
Verdict: **NEEDS CHANGES**

## Changes checked

- `docs/design.md` plus the complete tracked top-level `design/**` tree were consolidated under `docs/design/**`. Tree accounting is exact: 114 old `design/**` files plus `docs/design.md` became 115 files under `docs/design/**`; no tracked `design/**` or `docs/temp-plans/**` paths remain.
- Live references/build inputs were retargeted across `.design-sync`, Git attributes/ignores, docs, source comments, renderer Docker/project inputs, workspace documents and the design-system build script.
- All 21 `docs/temp-plans/**` files were removed. Completed/non-current plans have Git and ticket dispositions in KANMER-002 research. The still-actionable renderer integration direction is materially preserved in SIMPLI-015 research/files, including governing ADR/FRD work, application/DI/runtime seam, MCP consolidation, asset embedding/resource verification, documentation timing, and the SIMPLI-013/SIMPLI-014 re-scope.
- The redundant workbook deletion is supported by exact Git blob equality: both old paths were blob `0cdf08f4e3d1ab5ead54b5406ac6ebd40492bd0a`; only the canonical sibling remains and no tracked inbound reference named the deleted contacts path.
- Ignored `artifacts/` was correctly kept out of the PR. A fresh read-only check found the previously proposed planning/audit candidates absent and only one `tools` directory; active artifact roots remain. Git stores no empty directories.
- Process changes are confined to AGENTS.md/docs navigation and the link checker. Product behavior and operator-notes were not changed.

## Repository-required scope answers

1. **Did the plan miss anything implied by the ticket?** No. The plan covers reference cleanup, the atomic design move, temporary-plan disposition/Kanmer preservation, ignored-artifact audit, empty-directory handling, link/build updates, and verification.
2. **Did implementation miss anything in the plan?** Yes. Ordered step 4 and the zero-stale-consumer proof require every design-sync input to be correctly retargeted, but `.design-sync/config.json` contains a broken package-relative guidelines path.

## Comments and disposition

- **Blocking — design-sync authority path is wrong.** `guidelinesGlob` changed to `../../docs/design/README.md`. The existing convention and NOTES establish that this glob is relative to the design-system package. From the new package root `docs/design/system`, that value resolves to nonexistent `docs/docs/design/README.md`; the correct relative value is `../README.md`. The same incorrect value is asserted in `.design-sync/NOTES.md`. The reported `npm run build` pass does not exercise `guidelinesGlob`, so it cannot prove this consumer. **Disposition: filed as [[PR-001]], which blocks KANMER-002.**
- **Non-blocking — CI was not yet complete at review time.** Documentation, changes and reference-data checks were green; source-workspaces, unit, three SQL integration jobs and browser were still running. **Disposition: no ticket; merge remains prohibited until all required checks are green.**
- No open-questions document exists, so no unresolved operator question authorizes or blocks a review fix.

## Evidence checked

Ticket body; research; files; plan; checklist; open-questions; post-implementation report; SIMPLI-015 body/research/files; PR metadata and full Git diff against current `origin/dev`; old/new tree counts; stale-path searches; `git diff --check`; duplicate blob identity and inbound-reference searches; current ignored-artifact inventory; live PR check state.

Re-review after PR-001 lands. Do not merge this revision.
