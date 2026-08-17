# Files — TICK-195

## Where the change lands

| Path | Why |
|---|---|
| `scripts/Test-MarkdownPlacement.ps1` (new) | Own the diff-aware policy check, explicit base/head input, actionable diagnostics, and non-zero failure for unauthorized new Markdown paths. A new script avoids collision with KANMER-002's claimed link-checker edit. |
| `.github/workflows/ci.yml` | Make both comparison revisions available and invoke placement validation from the documentation job that already runs on every change set. |
| A focused non-UI PowerShell test/fixture under the repository's established script-test convention, chosen during planning | Prove allowed, forbidden, rename/copy, multiple-error, and invalid-comparison behavior without involving application or browser tests. If no durable convention exists after KANMER-002 lands, keep deterministic fixture verification in the ticket's verification commands rather than inventing a new top-level test system. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | Canonical repository rule for new Markdown placement and task ownership. Re-read after KANMER-002; do not edit its claimed governance changes in this ticket. |
| `docs/index.md` | Navigation/authority interpretation, including how canonical docs, design assets, references, and workspace-local docs are classified. Re-read after KANMER-002. |
| `scripts/Test-DocumentationLinks.ps1` | Existing link checker, exclusions, Windows path behavior, and a boundary not to duplicate or edit while KANMER-002 owns it. |
| `.github/workflows/ci.yml` | Always-running documentation lane, current shallow checkout, Windows runner, and the existing full-history diff precedent in the changes job. |
| `workspaces/AGENTS.md` and `workspaces/README.md` | Existing contract for independently buildable workspace documentation; use it to define any workspace exception rather than treating every arbitrary subtree as allowed. |
| `EPIC-001/context.md` | Prohibits edits to Web presentation, UI tests, `design/**`, and `.stitch/**`, and requires a fresh overlap check before implementation. |
| `KANMER-002` research/files documents | Establish the active claim and pending retirement of `docs/temp-plans/`, so the validator must target the landed policy rather than current text. |

## Ripple effects

- Pull requests adding or relocating Markdown will gain an always-running repository-policy gate with path-specific failures.
- The documentation job needs sufficient Git history, modestly increasing checkout transfer while avoiding application build lanes.
- KANMER-002 must land or publish its final placement decision first; TICK-200 should optimize CI only after this stable lane exists.
- Existing Markdown is grandfathered unless moved/copied into a new location; this avoids turning historical cleanup into unrelated blocking work.

## Out of scope

- `src/Pegasus.Web/**`, all UI-focused browser/snapshot tests, `design/**`, and `.stitch/**`.
- Editing `AGENTS.md`, `docs/index.md`, `docs/temp-plans/**`, or `scripts/Test-DocumentationLinks.ps1` while KANMER-002 owns those changes.
- Reorganizing existing Markdown, checking Markdown style/content, or repairing broken links.
- Azure, mailbox, Box, deployment, credential, or other external writes.

## Refresh — current file inventory after PR #379 and TICK-200

This inventory supersedes the earlier KANMER-002 sequencing notes.

### Where the change lands

| Path | Why |
|---|---|
| `scripts/Test-MarkdownPlacement.ps1` (new) | Diff-aware executable gate: accept explicit base/head and repository path, identify added/copied/renamed Markdown destinations, apply the final allowed-location policy, aggregate violations, and fail closed when comparison evidence is unavailable. |
| `scripts/Test-TestMarkdownPlacement.ps1` (new, or equivalently named paired regression script following the current `Test-CiChangeFlags.ps1` precedent) | Build temporary local Git histories and prove allowed PRD/FRD/ADR and registered-workspace additions, forbidden root/tooling/design/reference/task-plan additions, modification/deletion behavior, rename/copy destinations, multiple errors, and invalid/all-zero bases. |
| `.github/workflows/ci.yml` | Starting from TICK-200's merged workflow, give the always-running Windows documentation job both revisions and invoke the validator with pull-request or push event SHAs before/alongside the relative-link check. |

### Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | Final canonical placement rule and the requirement that task artifacts remain Kanmer ticket docs. |
| `docs/index.md` | Same final rule in the documentation authority index; no `docs/temp-plans/` destination remains. |
| `workspaces/README.md` | Exact registered workspace roots eligible for the workspace-local exception. |
| `workspaces/AGENTS.md` | Constraints on documentation and changes within those independently buildable imports. |
| `scripts/Test-DocumentationLinks.ps1` | Existing, separate Markdown link concern and Windows behavior; do not merge placement logic into it. |
| `scripts/Get-CiChangeFlags.ps1` and `scripts/Test-CiChangeFlags.ps1` | Current post-TICK-200 precedent for separating deterministic policy logic from executable regression coverage. |
| `.github/workflows/ci.yml` | Final post-TICK-200 jobs, event handling, full-history precedent in `changes`, and shallow `documentation` checkout to adjust. |
| `EPIC-001/context.md` | UI revamp exclusion and the mandatory fresh overlap check before implementation. |

### Ripple effects

- Every pull request and push to `main` gains a path-specific gate for newly placed Markdown.
- The documentation checkout must fetch enough history to prove the event comparison; application lanes remain unaffected.
- Existing out-of-policy Markdown is not retroactively failed. A move/copy to a new destination is checked.
- Future Kanmer/plugin setup that wants to add Markdown under managed tooling trees must first change the governing rule or update only existing files; the validator must not invent an exemption.
- TICK-200 is already merged, so TICK-195 must preserve its change classifier, shard assignment, infrastructure lane, and wall-clock optimizations while editing the shared workflow.

### Out of scope

- `src/Pegasus.Web/**`, UI-focused browser/snapshot tests, `docs/design/**`, and `.stitch/**`.
- Recreating `docs/temp-plans/` or writing any task plan outside the Kanmer ticket.
- Editing the placement authority in `AGENTS.md` or `docs/index.md`.
- Adding exemptions for tooling, design, reference evidence, or arbitrary Markdown locations.
- Markdown style/content validation, existing-file cleanup, and relative-link repair.
- Azure, mailbox, Box, deployment, credential, or other external writes.
