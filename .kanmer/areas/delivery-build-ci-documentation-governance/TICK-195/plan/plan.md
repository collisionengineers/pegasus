# Plan — TICK-195: Validate new Markdown placement in CI

## Approach

Add a dedicated PowerShell validator that treats Git as the source of truth for placement events between two explicit commits. It will validate added Markdown paths plus copy and rename destinations against the three canonical documentation trees and the two registered workspace roots, report all violations together, and fail closed when either revision or the comparison is invalid. Keeping this separate from the existing link checker preserves a single concern per script and follows the tested script/regression pattern already established by TICK-200.

## Governing docs

No PRD, FRD, or ADR is linked because this is repository-governance enforcement rather than product capability behavior or an architectural decision. The implementation enforces the binding new-Markdown placement rule in `AGENTS.md` and `docs/index.md` without changing either authority. The ticket's `docs_todo` records that no new governing product document is required.

## Steps

1. Add `scripts/Test-MarkdownPlacement.ps1` with mandatory explicit base/head revisions and an optional repository root; validate both commits, obtain the Git name-status comparison, check Markdown additions and copy/rename destinations, allow only `docs/prd/**`, `docs/frd/**`, `docs/adr/**`, `workspaces/document-extraction/**`, and `workspaces/report-renderer/**`, aggregate invalid paths, and fail closed on invalid or unavailable evidence.
2. Add `scripts/Test-TestMarkdownPlacement.ps1` using disposable local Git repositories to prove allowed canonical and workspace additions; rejected root, tooling, design, reference, and retired task-plan paths; grandfathered modifications/deletions; checked rename/copy destinations; multiple-error reporting; and invalid/all-zero comparison failures.
3. Update only the existing Windows `documentation` job in `.github/workflows/ci.yml`: fetch full history, select pull-request base/head or push before/head SHAs, run the focused regression script, then run the validator before the existing link check. Preserve every TICK-200 classifier, shard, infrastructure-lane, and workflow optimization.
4. Run the focused regression script and direct repository-range smoke checks, inspect the diff for UI-revamp exclusions and accidental task-plan files, then commit, push, open a PR targeting `dev`, and record the implementation report and traceability.

## Verification

Run `./scripts/Test-TestMarkdownPlacement.ps1` for deterministic behavior coverage. Run `./scripts/Test-MarkdownPlacement.ps1 -Base <origin/dev parent> -Head <ticket head>` against the real repository and confirm the ticket's allowed script/workflow-only change passes. Exercise an invalid revision and confirm a non-zero exit. Inspect `git diff --check`, `git status --short`, and the changed-path list to prove no Web, UI-test, `docs/design/**`, `.stitch/**`, or `docs/temp-plans/**` changes.

## Risks / open questions

- Git rename/copy detection is heuristic; explicitly enable both and test destinations in constructed histories. Any reported R/C destination is treated as new placement.
- Shallow CI history could make a valid event unverifiable; full checkout history in the documentation job mitigates this, while the validator still fails closed if revisions are absent.
- Native Git failures or malformed name-status records must not degrade to a pass; check exit status and reject unparseable placement records.
- No open user question remains. Adding exemptions for tooling, design, reference, or arbitrary workspace trees is outside this ticket and requires a separate governance change.
