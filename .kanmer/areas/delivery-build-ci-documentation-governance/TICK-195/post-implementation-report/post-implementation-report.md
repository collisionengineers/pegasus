# Post-implementation report — TICK-195

## Summary

Implemented an always-running CI guard for new Markdown placement. The validator uses explicit Git base/head evidence, checks additions plus copy/rename destinations, permits only canonical PRD/FRD/ADR trees and the two registered workspace roots, aggregates violations, and fails closed when comparison evidence is invalid. Existing Markdown modifications and deletions remain grandfathered.

## Changes

| File | Change | Why |
|---|---|---|
| `scripts/Test-MarkdownPlacement.ps1` | added | Own the explicit-range Git comparison, A/C/R destination classification, path policy, aggregate diagnostics, and fail-closed behavior. |
| `scripts/Test-TestMarkdownPlacement.ps1` | added | Prove policy behavior in disposable local Git histories without application, browser, cloud, or UI dependencies. |
| `.github/workflows/ci.yml` | modified | Give the Windows documentation job full history and event-specific revisions, then run regression and placement checks before the existing link check. |

## Governing docs

No PRD, FRD, or ADR is linked because this change enforces repository governance rather than product behavior or architecture. It implements the existing new-Markdown rules in `AGENTS.md` and `docs/index.md` without modifying them. No exemption was invented for tooling, design, reference, root, or retired task-plan paths.

## Risks / follow-ups

- Git rename/copy classification remains heuristic by Git design; the validator enables rename detection and harder copy detection, and regression histories verify that any detected destination is checked.
- The documentation checkout now fetches full history. This is necessary to prove explicit event revisions and is isolated from application build lanes.
- `origin/dev` advanced during execution with the UI-design reconciliation. The current base was merged through the repository-approved path; the PR diff against current `dev` contains only the workflow and two scripts, so no UI/design files are ticket-owned changes.
- CI results on PR #384 remain the independent pre-merge evidence for the reviewer.

## Verification hand-off

On merged `dev`/the repository verification branch, run:

- `./scripts/Test-TestMarkdownPlacement.ps1` — expect `Markdown placement regression tests passed.`
- `./scripts/Test-MarkdownPlacement.ps1 -Base <parent> -Head HEAD` for a comparison containing only allowed/non-Markdown changes — expect a pass line.
- `./scripts/Test-DocumentationLinks.ps1` — expect every relative Markdown link to resolve.
- Invoke the validator with an unavailable base and confirm it throws `not an available commit`.
- Inspect the merged diff and confirm no `src/Pegasus.Web/**`, UI browser/snapshot tests, `docs/design/**`, `.stitch/**`, or `docs/temp-plans/**` path is owned by TICK-195.
